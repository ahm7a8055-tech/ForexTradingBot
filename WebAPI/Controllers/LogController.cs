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
using System.IO;
using System.Text.RegularExpressions;
using Shared.Security; // For SecureExceptionSanitizer

namespace WebAPI.Controllers
{
    [ApiController]
    [Route("api/logs")]
    [Authorize(Roles = "Admin")]
    public class LogController : ControllerBase
    {
        #region Fields and Constructor
        private readonly IAdminService _adminService;
        private readonly ILogger<LogController> _logger;

        public LogController(IAdminService adminService, ILogger<LogController> logger)
        {
            _adminService = adminService;
            _logger = logger;
        }
        #endregion

        #region Security Validation Methods
        /// <summary>
        /// Sanitizes user input for safe logging by removing newlines and other problematic characters.
        /// </summary>
        /// <param name="input">The user input to sanitize</param>
        /// <returns>Sanitized string safe for logging</returns>
        private static string SanitizeForLogging(string? input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return "[EMPTY_INPUT]";

            // Remove newlines, carriage returns, and other problematic characters
            var sanitized = input
                .Replace("\r", "")
                .Replace("\n", "")
                .Replace("\t", " ")
                .Replace("\0", ""); // Null characters

            // Remove any remaining control characters
            sanitized = Regex.Replace(sanitized, @"[\x00-\x1F\x7F]", "");

            // Limit length to prevent log flooding
            if (sanitized.Length > 200)
            {
                sanitized = sanitized.Substring(0, 197) + "...";
            }

            return sanitized;
        }

        /// <summary>
        /// Creates a secure error response that doesn't expose sensitive information to clients.
        /// </summary>
        /// <param name="statusCode">The HTTP status code</param>
        /// <param name="userMessage">User-friendly message (no sensitive data)</param>
        /// <param name="internalErrorId">Optional internal error ID for tracking</param>
        /// <returns>Secure error response</returns>
        private IActionResult CreateSecureErrorResponse(int statusCode, string userMessage, string? internalErrorId = null)
        {
            var response = new
            {
                Error = userMessage,
                ErrorId = internalErrorId ?? Guid.NewGuid().ToString("N")[..8], // Short error ID for tracking
                Timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
            };

            return StatusCode(statusCode, response);
        }

        /// <summary>
        /// Validates and sanitizes file names to prevent path traversal and other attacks.
        /// </summary>
        /// <param name="fileName">The file name to validate</param>
        /// <returns>Validated file name or null if invalid</returns>
        private string? ValidateFileName(string? fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                _logger.LogWarning("File name is null or empty.");
                return null;
            }

            // Sanitize the file name
            var sanitizedFileName = SanitizeForLogging(fileName);

            // Check for path traversal attempts
            if (sanitizedFileName.Contains("..") || 
                sanitizedFileName.Contains("\\") || 
                sanitizedFileName.Contains("/") ||
                sanitizedFileName.Contains(":"))
            {
                _logger.LogWarning("Path traversal attempt detected in file name: {SanitizedFileName}", sanitizedFileName);
                return null;
            }

            // Validate file name format (should be like log-YYYYMMDD.txt)
            if (!Regex.IsMatch(sanitizedFileName, @"^log-\d{8}\.txt$"))
            {
                _logger.LogWarning("Invalid log file name format: {SanitizedFileName}", sanitizedFileName);
                return null;
            }

            return sanitizedFileName;
        }

        /// <summary>
        /// Validates line count parameter to prevent resource exhaustion.
        /// </summary>
        /// <param name="lineCount">The line count to validate</param>
        /// <returns>Validated line count or null if invalid</returns>
        private int? ValidateLineCount(int? lineCount)
        {
            if (lineCount.HasValue)
            {
                // Limit line count to prevent resource exhaustion
                if (lineCount.Value <= 0 || lineCount.Value > 10000)
                {
                    _logger.LogWarning("Invalid line count requested: {LineCount}. Must be between 1 and 10000.", lineCount.Value);
                    return null;
                }
            }

            return lineCount;
        }
        #endregion

        #region API Endpoints
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
                
                // SECURITY: Sanitize file names before logging
                var sanitizedFiles = files?.Select(f => SanitizeForLogging(f)).ToList() ?? new List<string>();
                _logger.LogInformation("Successfully listed {Count} log files.", sanitizedFiles.Count);
                
                return Ok(files);
            }
            catch (Exception ex)
            {
                // SECURITY: Use SecureExceptionSanitizer for logging exceptions (now uses encryption)
                var sanitizedException = SecureExceptionSanitizer.SanitizeForLogging(ex);
                var errorId = Guid.NewGuid().ToString("N")[..8];
                _logger.LogError(sanitizedException, "Error listing log files. ErrorId: {ErrorId}", errorId);
                
                // SECURITY: Return sanitized error response without exposing sensitive information
                return CreateSecureErrorResponse(
                    StatusCodes.Status500InternalServerError, 
                    "An error occurred while listing log files. Please try again later.",
                    errorId);
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
        /// <param name="cancellationToken">Cancellation token</param>
        [HttpGet("view/{fileName}")]
        [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ViewLogFile(string fileName, [FromQuery] int? lineCount, CancellationToken cancellationToken)
        {
            // SECURITY: Validate and sanitize input parameters
            var validatedFileName = ValidateFileName(fileName);
            var validatedLineCount = ValidateLineCount(lineCount);

            if (validatedFileName == null)
            {
                return CreateSecureErrorResponse(
                    StatusCodes.Status400BadRequest, 
                    "Invalid file name format. File name must be in format: log-YYYYMMDD.txt");
            }

            if (lineCount.HasValue && validatedLineCount == null)
            {
                return CreateSecureErrorResponse(
                    StatusCodes.Status400BadRequest, 
                    "Invalid line count. Must be between 1 and 10000.");
            }

            // SECURITY: Use sanitized values for logging
            var sanitizedFileName = SanitizeForLogging(validatedFileName);
            var sanitizedLineCount = validatedLineCount?.ToString() ?? "null";
            
            _logger.LogInformation("Attempting to view log file: {SanitizedFileName}, LineCount: {SanitizedLineCount}", 
                sanitizedFileName, sanitizedLineCount);

            try
            {
                var content = await _adminService.GetLogFileContentAsync(validatedFileName, validatedLineCount, cancellationToken);
                if (content == null)
                {
                    _logger.LogWarning("Log file not found or access denied during view: {SanitizedFileName}", sanitizedFileName);
                    return CreateSecureErrorResponse(
                        StatusCodes.Status404NotFound, 
                        "Log file not found or access denied.");
                }

                // SECURITY: Validate content before returning
                if (content.Length > 10 * 1024 * 1024) // 10MB limit
                {
                    _logger.LogWarning("Log file content too large: {SanitizedFileName}, Size: {Size} bytes", 
                        sanitizedFileName, content.Length);
                    return CreateSecureErrorResponse(
                        StatusCodes.Status400BadRequest, 
                        "Log file content is too large to display.");
                }

                _logger.LogInformation("Successfully retrieved log file content: {SanitizedFileName}, Size: {Size} bytes", 
                    sanitizedFileName, content.Length);

                // Return as plain text. Client can put it in a <pre> tag.
                return Content(content, "text/plain");
            }
            catch (Exception ex)
            {
                // SECURITY: Use SecureExceptionSanitizer for logging exceptions (now uses encryption)
                var sanitizedException = SecureExceptionSanitizer.SanitizeForLogging(ex);
                var errorId = Guid.NewGuid().ToString("N")[..8];
                _logger.LogError(sanitizedException, "Error viewing log file: {SanitizedFileName}. ErrorId: {ErrorId}", 
                    sanitizedFileName, errorId);
                
                // SECURITY: Return sanitized error response without exposing sensitive information
                return CreateSecureErrorResponse(
                    StatusCodes.Status500InternalServerError, 
                    "An error occurred while viewing log file. Please try again later.",
                    errorId);
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
                    // SECURITY: Sanitize error message before logging
                    var sanitizedErrorMessage = SanitizeForLogging(errorMessage);
                    _logger.LogWarning("Failed to get log files for ZIP download: {SanitizedErrorMessage}", sanitizedErrorMessage);
                    
                    // SECURITY: Return sanitized error response without exposing sensitive information
                    return CreateSecureErrorResponse(
                        StatusCodes.Status404NotFound, 
                        "No log files found or an error occurred while preparing the download.");
                }

                // SECURITY: Sanitize file name before logging
                var sanitizedFileName = SanitizeForLogging(fileName);
                _logger.LogInformation("Successfully prepared log files ZIP for download: {SanitizedFileName}, Size: {Size} bytes", 
                    sanitizedFileName, zipContents.Length);

                // SECURITY: Validate file size before returning
                if (zipContents.Length > 100 * 1024 * 1024) // 100MB limit
                {
                    _logger.LogWarning("ZIP file too large for download: {SanitizedFileName}, Size: {Size} bytes", 
                        sanitizedFileName, zipContents.Length);
                    return CreateSecureErrorResponse(
                        StatusCodes.Status400BadRequest, 
                        "ZIP file is too large for download.");
                }

                return File(zipContents, "application/zip", fileName);
            }
            catch (Exception ex)
            {
                // SECURITY: Use SecureExceptionSanitizer for logging exceptions (now uses encryption)
                var sanitizedException = SecureExceptionSanitizer.SanitizeForLogging(ex);
                var errorId = Guid.NewGuid().ToString("N")[..8];
                _logger.LogError(sanitizedException, "Error downloading log files as ZIP. ErrorId: {ErrorId}", errorId);
                
                // SECURITY: Return sanitized error response without exposing sensitive information
                return CreateSecureErrorResponse(
                    StatusCodes.Status500InternalServerError, 
                    "An error occurred while preparing log files for download. Please try again later.",
                    errorId);
            }
        }
        #endregion
    }
}
