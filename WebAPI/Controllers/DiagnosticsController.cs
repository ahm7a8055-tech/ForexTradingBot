using Application.DTOs.Diagnostics;
using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;

namespace WebAPI.Controllers
{
    [ApiController]
    [Route("api/diagnostics")]
    [Authorize] // Consider if this needs admin role or can be accessed by any authenticated user
    public class DiagnosticsController : ControllerBase
    {
        private readonly IDiagnosticsService _diagnosticsService;
        private readonly ILogger<DiagnosticsController> _logger;

        public DiagnosticsController(IDiagnosticsService diagnosticsService, ILogger<DiagnosticsController> logger)
        {
            _diagnosticsService = diagnosticsService ?? throw new ArgumentNullException(nameof(diagnosticsService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [HttpGet("connectivity-status")]
        [ProducesResponseType(typeof(ConnectivityStatusDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetConnectivityStatus(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Attempting to retrieve connectivity status.");
            try
            {
                var status = await _diagnosticsService.CheckConnectivityAsync(cancellationToken);
                _logger.LogInformation(
                    "Connectivity status retrieved: DB Connected - {DbStatus}, Telegram API Connected - {TelegramStatus}",
                    status.CanConnectToDatabase,
                    status.CanAccessTelegramApi);
                return Ok(status);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving connectivity status.");
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while checking system connectivity.");
            }
        }
    }
}
