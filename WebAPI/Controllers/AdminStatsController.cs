using Application.DTOs.Admin;
using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace WebAPI.Controllers
{
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

        [HttpGet("stats")]
        [ProducesResponseType(typeof(AdminDashboardStatsDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetAdminStats(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Attempting to retrieve admin dashboard statistics.");
            try
            {
                var stats = await _adminService.GetAdminDashboardStatsAsync(cancellationToken);
                _logger.LogInformation("Admin dashboard statistics retrieved successfully.");
                return Ok(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving admin dashboard statistics.");
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while retrieving admin dashboard statistics.");
            }
        }
    }
}
