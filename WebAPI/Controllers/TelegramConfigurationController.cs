using Application.DTOs.Telegram;
using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Controllers
{
    [ApiController]
    [Route("api/telegram")]
    [Authorize] // TODO: Consider specific admin role if available e.g., [Authorize(Roles = AppRoles.Administrator)]
    public class TelegramConfigurationController : ControllerBase
    {
        private readonly ISettingsService _settingsService;
        private readonly ILogger<TelegramConfigurationController> _logger;

        public TelegramConfigurationController(ISettingsService settingsService, ILogger<TelegramConfigurationController> logger)
        {
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        // Bot Settings
        [HttpGet("bot-settings")]
        [ProducesResponseType(typeof(TelegramBotSettingsDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetBotSettings(CancellationToken cancellationToken)
        {
            try
            {
                TelegramBotSettingsDto settings = await _settingsService.GetTelegramBotSettingsAsync(cancellationToken);
                return Ok(settings);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving Telegram bot settings.");
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while retrieving bot settings.");
            }
        }

        [HttpPost("bot-settings")] // Could also be PUT
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UpdateBotSettings([FromBody] TelegramBotSettingsDto settings, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new ValidationProblemDetails(ModelState));
            }

            try
            {
                await _settingsService.UpdateTelegramBotSettingsAsync(settings, cancellationToken);
                _logger.LogInformation("Telegram bot settings updated successfully.");
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating Telegram bot settings.");
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while updating bot settings.");
            }
        }

        // Client Settings
        [HttpGet("client-settings")]
        [ProducesResponseType(typeof(TelegramClientSettingsDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetClientSettings(CancellationToken cancellationToken)
        {
            try
            {
                TelegramClientSettingsDto settings = await _settingsService.GetTelegramClientSettingsAsync(cancellationToken);
                return Ok(settings);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving Telegram client settings.");
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while retrieving client settings.");
            }
        }

        [HttpPost("client-settings")] // Could also be PUT
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UpdateClientSettings([FromBody] TelegramClientSettingsDto settings, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new ValidationProblemDetails(ModelState));
            }

            try
            {
                await _settingsService.UpdateTelegramClientSettingsAsync(settings, cancellationToken);
                _logger.LogInformation("Telegram client settings updated successfully.");
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating Telegram client settings.");
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while updating client settings.");
            }
        }
    }
}
