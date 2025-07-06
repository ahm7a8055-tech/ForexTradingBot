using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using StackExchange.Redis;
using System;
using System.ComponentModel.DataAnnotations;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Application.Interfaces; // Required for IDiagnosticsService if used directly

namespace WebAPI.Controllers
{
    [ApiController]
    [Route("api/config")]
    [Authorize(Roles = "Admin")]
    public class ConfigController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<ConfigController> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        // private readonly IDiagnosticsService _diagnosticsService; // Option to use existing service

        public ConfigController(
            IConfiguration configuration,
            ILogger<ConfigController> logger,
            IHttpClientFactory httpClientFactory)
            // IDiagnosticsService diagnosticsService) // Option
        {
            _configuration = configuration;
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            // _diagnosticsService = diagnosticsService; // Option
        }

        public class TestConfigRequestModel
        {
            [Required]
            public string? BotToken { get; set; }

            [Required]
            public string? DbConn { get; set; }

            public string? RedisConn { get; set; } // Optional
        }

        public class TestConfigResponseModel
        {
            public string DatabaseStatus { get; set; } = "Not Tested";
            public string? DatabaseError { get; set; }
            public string RedisStatus { get; set; } = "Not Tested";
            public string? RedisError { get; set; }
            public string TelegramStatus { get; set; } = "Not Tested";
            public string? TelegramError { get; set; }
            public string? BotUsername { get; set; }
        }

        [HttpPost("test")]
        [ProducesResponseType(typeof(TestConfigResponseModel), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> TestConfiguration([FromBody] TestConfigRequestModel model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var response = new TestConfigResponseModel();

            // Test Database Connection
            try
            {
                // Note: IDiagnosticsService.CheckConnectivityAsync() uses the configured connection string.
                // Here we test the one provided by the user.
                _logger.LogInformation("Testing database connection to: {DbConn}", model.DbConn?.Substring(0, Math.Min(model.DbConn.Length, 20)) + "..."); // Log only prefix
                await using var connection = new NpgsqlConnection(model.DbConn);
                await connection.OpenAsync();
                await connection.CloseAsync();
                response.DatabaseStatus = "OK";
                _logger.LogInformation("Database connection test successful.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database connection test failed.");
                response.DatabaseStatus = "Error";
                response.DatabaseError = ex.Message;
            }

            // Test Redis Connection
            if (!string.IsNullOrWhiteSpace(model.RedisConn))
            {
                try
                {
                    _logger.LogInformation("Testing Redis connection to: {RedisConn}", model.RedisConn);
                    var redis = await ConnectionMultiplexer.ConnectAsync(model.RedisConn);
                    if (redis.IsConnected)
                    {
                        await redis.GetDatabase().PingAsync();
                        response.RedisStatus = "OK";
                        _logger.LogInformation("Redis connection test successful.");
                    }
                    else
                    {
                        response.RedisStatus = "Error";
                        response.RedisError = "Failed to connect to Redis.";
                        _logger.LogWarning("Redis connection test failed: Not connected.");
                    }
                    await redis.CloseAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Redis connection test failed.");
                    response.RedisStatus = "Error";
                    response.RedisError = ex.Message;
                }
            }
            else
            {
                response.RedisStatus = "Not Provided";
            }

            // Test Telegram Bot Token
            try
            {
                _logger.LogInformation("Testing Telegram Bot Token.");
                var client = _httpClientFactory.CreateClient();
                var telegramApiResponse = await client.GetAsync($"https://api.telegram.org/bot{model.BotToken}/getMe");

                if (telegramApiResponse.IsSuccessStatusCode)
                {
                    var content = await telegramApiResponse.Content.ReadAsStringAsync();
                    var jsonDoc = JsonDocument.Parse(content);
                    if (jsonDoc.RootElement.TryGetProperty("result", out var resultElement) &&
                        resultElement.TryGetProperty("username", out var usernameElement))
                    {
                        response.BotUsername = usernameElement.GetString();
                    }
                    response.TelegramStatus = "OK";
                    _logger.LogInformation("Telegram Bot Token test successful. Bot Username: {BotUsername}", response.BotUsername);
                }
                else
                {
                    var errorContent = await telegramApiResponse.Content.ReadAsStringAsync();
                    _logger.LogWarning("Telegram Bot Token test failed. Status: {StatusCode}, Response: {ErrorContent}", telegramApiResponse.StatusCode, errorContent);
                    response.TelegramStatus = "Error";
                    response.TelegramError = $"Telegram API returned {telegramApiResponse.StatusCode}. Details: {errorContent}";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Telegram Bot Token test failed.");
                response.TelegramStatus = "Error";
                response.TelegramError = ex.Message;
            }

            return Ok(response);
        }

        public class SaveConfigRequestModel
        {
            [Required]
            public string? BotToken { get; set; }
            [Required]
            public string? DbConn { get; set; }
            public string? RedisConn { get; set; }
        }

        [HttpPost("save")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult SaveConfiguration([FromBody] SaveConfigRequestModel model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // CRITICAL SECURITY NOTE:
            // In a real-world application, NEVER write sensitive configuration like Bot Tokens
            // or Connection Strings directly to appsettings.json, especially in production.
            // This endpoint is a PLACEHOLDER to simulate a save operation.
            //
            // Proper implementations should:
            // 1. Store these configurations in secure, managed stores such as:
            //    - Azure Key Vault
            //    - HashiCorp Vault
            //    - Kubernetes Secrets
            //    - Environment Variables (configured securely on the host/platform)
            // 2. The application should then read these configurations at startup from these secure sources.
            // 3. If dynamic updates are needed (rare for such core settings), the application
            //    should be designed to reload configuration from these secure stores,
            //    potentially via a secure management API or a signaling mechanism (e.g., Azure App Configuration).
            //
            // This current logging is for demonstration purposes only for this project.
            _logger.LogWarning("Received configuration to save (PLACEHOLDER - NOT SAVING TO APPSETTINGS.JSON):");
            _logger.LogWarning("BotToken: {BotToken}", model.BotToken); // Be cautious logging tokens, even here.
            _logger.LogWarning("DbConn: {DbConn}", model.DbConn);
            _logger.LogWarning("RedisConn: {RedisConn}", model.RedisConn);

            // Simulate successful save
            return Ok(new { Message = "Configuration received (placeholder save). See server logs for details. Ensure real-world implementation uses secure configuration stores." });
        }
    }
}
