using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using System.Threading;

namespace WebAPI.Controllers
{
    [ApiController]
    [Route("api/logs")]
    [Authorize(Roles = "Admin")]
    public class LogController : ControllerBase
    {
        private readonly IAdminService _adminService;
        private readonly ILogger<LogController> _logger;

        public LogController(IAdminService adminService, ILogger<LogController> logger)
        {
            _adminService = adminService;
            _logger = logger;
        }

        /// <summary>
        /// Lists available log files.
        /// </summary>
        [HttpGet("list")]
        [ProducesResponseType(typeof(List<string>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ListLogFiles(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Attempting to list log files.");
            try
            {
                var files = await _adminService.ListLogFilesAsync(cancellationToken);
                return Ok(files);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing log files.");
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while listing log files.");
            }
        }

        public class ViewLogRequest
        {
            [Required]
            public string? FileName { get; set; }
            public int? LineCount { get; set; } // Optional, last N lines
        }

        /// <summary>
        /// Gets the content of a specific log file.
        /// </summary>
        /// <param name="fileName">The name of the log file (e.g., log-20231027.txt).</param>
        /// <param name="lineCount">Optional. If specified, returns the last N lines of the file.</param>
        [HttpGet("view/{fileName}")]
        [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ViewLogFile(string fileName, [FromQuery] int? lineCount, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return BadRequest("File name cannot be empty.");
            }

            _logger.LogInformation("Attempting to view log file: {FileName}, LineCount: {LineCount}", fileName, lineCount);
            try
            {
                var content = await _adminService.GetLogFileContentAsync(fileName, lineCount, cancellationToken);
                if (content == null)
                {
                    _logger.LogWarning("Log file not found or access denied during view: {FileName}", fileName);
                    return NotFound($"Log file '{fileName}' not found or access denied.");
                }
                // Return as plain text. Client can put it in a <pre> tag.
                return Content(content, "text/plain");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error viewing log file: {FileName}", fileName);
                return StatusCode(StatusCodes.Status500InternalServerError, $"An error occurred while viewing log file '{fileName}'.");
            }
        }

        /// <summary>
        /// Downloads all log files as a ZIP archive.
        /// </summary>
        [HttpGet("zip")]
        [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DownloadLogsZip(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Attempting to download all log files as ZIP.");
            try
            {
                var (zipContents, fileName, errorMessage) = await _adminService.GetLogFilesAsZipAsync(cancellationToken);

                if (!string.IsNullOrEmpty(errorMessage) || zipContents == null || zipContents.Length == 0)
                {
                    _logger.LogWarning("Failed to get log files for ZIP download: {ErrorMessage}", errorMessage);
                    return NotFound(errorMessage ?? "No log files found or an error occurred.");
                }

                _logger.LogInformation("Successfully prepared log files ZIP for download: {FileName}", fileName);
                return File(zipContents, "application/zip", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading log files as ZIP.");
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while preparing log files for download.");
            }
        }
    }
}
