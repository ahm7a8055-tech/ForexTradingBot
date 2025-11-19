using Application.DTOs.Admin;
using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Controllers
{
    /// <summary>
    /// Provides administrative statistics for the dashboard.
    /// </summary>
    [ApiController]
    [Route("api/admin")]
    [Authorize(Roles = "Admin")]
    public class AdminStatsController : ControllerBase
    {
        private readonly IAdminService _adminService;
        private readonly ILogger<AdminStatsController> _logger;

        public AdminStatsController(IAdminService adminService, ILogger<AdminStatsController> logger)
        {
            _adminService = adminService ?? throw new ArgumentNullException(nameof(adminService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Retrieves key statistics for the admin dashboard.
        /// </summary>
        /// <remarks>
        /// This endpoint provides a consolidated view of metrics like total users,
        /// signals sent today, and other high-level operational data.
        /// </remarks>
        /// <param name="cancellationToken">A token to cancel the operation if the request is aborted.</param>
        /// <returns>An object containing dashboard statistics.</returns>
        [HttpGet("stats")]
        [ProducesResponseType(typeof(AdminDashboardStatsDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetAdminStats(CancellationToken cancellationToken)
        {
            _logger.LogInformation("API Request: Attempting to retrieve admin dashboard statistics.");
            try
            {
                AdminDashboardStatsDto stats = await _adminService.GetAdminDashboardStatsAsync(cancellationToken);
                _logger.LogInformation("API Success: Admin dashboard statistics retrieved successfully.");
                return Ok(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "API Error: An unexpected error occurred while retrieving admin dashboard statistics.");
                return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
                {
                    Title = "Internal Server Error",
                    Detail = "An unexpected error occurred on the server while processing your request."
                });
            }
        }
    }
}