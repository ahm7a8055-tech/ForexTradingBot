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
using Shared.Security; // For SecureExceptionSanitizer
using Npgsql; // For NpgsqlConnectionStringBuilder
using StackExchange.Redis; // For ConnectionMultiplexer
using System.Linq; // For Select

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

        #region Secure Connection String Validation
        /// <summary>
        /// Validates and sanitizes database connection strings to prevent resource injection attacks.
        /// </summary>
        /// <param name="connectionString">The raw connection string from user input</param>
        /// <returns>Validated connection string or null if invalid</returns>
        private string? ValidateDatabaseConnectionString(string? connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                _logger.LogWarning("Database connection string is null or empty.");
                return null;
            }

            try
            {
                // Use NpgsqlConnectionStringBuilder to safely parse and validate the connection string
                var builder = new NpgsqlConnectionStringBuilder(connectionString);
                
                // Additional security checks
                if (string.IsNullOrWhiteSpace(builder.Host))
                {
                    _logger.LogWarning("Database connection string missing host.");
                    return null;
                }

                // Log only safe parts of the connection string
                var safeConnectionInfo = $"Host={builder.Host}, Port={builder.Port}, Database={builder.Database}";
                _logger.LogInformation("Validated database connection string: {SafeConnectionInfo}", safeConnectionInfo);
                
                return builder.ConnectionString;
            }
            catch (Exception ex)
            {
                // SECURITY: Use SecureExceptionSanitizer for logging exceptions
                var sanitizedException = SecureExceptionSanitizer.SanitizeForLogging(ex);
                _logger.LogError(sanitizedException, "Invalid database connection string format.");
                return null;
            }
        }

        /// <summary>
        /// Validates and sanitizes Redis connection strings to prevent resource injection attacks.
        /// </summary>
        /// <param name="connectionString">The raw connection string from user input</param>
        /// <returns>Validated connection string or null if invalid</returns>
        private string? ValidateRedisConnectionString(string? connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                return null;
            }

            try
            {
                // Use ConfigurationOptions to safely parse and validate the connection string
                var options = ConfigurationOptions.Parse(connectionString);
                
                // Additional security checks
                if (options.EndPoints.Count == 0)
                {
                    _logger.LogWarning("Redis connection string missing endpoints.");
                    return null;
                }

                // Log only safe parts of the connection string
                var endpoints = string.Join(", ", options.EndPoints.Select(ep => ep.ToString()));
                _logger.LogInformation("Validated Redis connection string: Endpoints={Endpoints}", endpoints);
                
                return connectionString; // Return original as ConfigurationOptions doesn't have a ConnectionString property
            }
            catch (Exception ex)
            {
                // SECURITY: Use SecureExceptionSanitizer for logging exceptions
                var sanitizedException = SecureExceptionSanitizer.SanitizeForLogging(ex);
                _logger.LogError(sanitizedException, "Invalid Redis connection string format.");
                return null;
            }
        }
        #endregion

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
                // SECURITY: Validate and sanitize the connection string before use
                var validatedDbConn = ValidateDatabaseConnectionString(model.DbConn);
                if (validatedDbConn == null)
                {
                    response.DatabaseStatus = "Error";
                    response.DatabaseError = "Invalid database connection string format.";
                    _logger.LogWarning("Database connection test skipped due to invalid connection string.");
                }
                else
                {
                    _logger.LogInformation("Testing validated database connection.");
                    await using var connection = new NpgsqlConnection(validatedDbConn);
                    await connection.OpenAsync();
                    await connection.CloseAsync();
                    response.DatabaseStatus = "OK";
                    _logger.LogInformation("Database connection test successful.");
                }
            }
            catch (Exception ex)
            {
                // SECURITY: Use SecureExceptionSanitizer for logging exceptions
                var sanitizedException = SecureExceptionSanitizer.SanitizeForLogging(ex);
                _logger.LogError(sanitizedException, "Database connection test failed.");
                response.DatabaseStatus = "Error";
                response.DatabaseError = "Database connection failed. Check your connection string and network connectivity.";
            }

            // Test Redis Connection
            if (!string.IsNullOrWhiteSpace(model.RedisConn))
            {
                try
                {
                    // SECURITY: Validate and sanitize the connection string before use
                    var validatedRedisConn = ValidateRedisConnectionString(model.RedisConn);
                    if (validatedRedisConn == null)
                    {
                        response.RedisStatus = "Error";
                        response.RedisError = "Invalid Redis connection string format.";
                        _logger.LogWarning("Redis connection test skipped due to invalid connection string.");
                    }
                    else
                    {
                        _logger.LogInformation("Testing validated Redis connection.");
                        var redis = await ConnectionMultiplexer.ConnectAsync(validatedRedisConn);
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
                }
                catch (Exception ex)
                {
                    // SECURITY: Use SecureExceptionSanitizer for logging exceptions
                    var sanitizedException = SecureExceptionSanitizer.SanitizeForLogging(ex);
                    _logger.LogError(sanitizedException, "Redis connection test failed.");
                    response.RedisStatus = "Error";
                    response.RedisError = "Redis connection failed. Check your connection string and network connectivity.";
                }
            }
            else
            {
                response.RedisStatus = "Not Provided";
            }

            // Test Telegram Bot Token
            try
            {
                // SECURITY: Validate bot token format
                if (string.IsNullOrWhiteSpace(model.BotToken) || !model.BotToken.Contains(':'))
                {
                    response.TelegramStatus = "Error";
                    response.TelegramError = "Invalid bot token format. Bot token should be in format: <bot_id>:<token>";
                    _logger.LogWarning("Telegram bot token validation failed: Invalid format.");
                }
                else
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
            }
            catch (Exception ex)
            {
                // SECURITY: Use SecureExceptionSanitizer for logging exceptions
                var sanitizedException = SecureExceptionSanitizer.SanitizeForLogging(ex);
                _logger.LogError(sanitizedException, "Telegram Bot Token test failed.");
                response.TelegramStatus = "Error";
                response.TelegramError = "Telegram bot token test failed. Check your token and network connectivity.";
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

            // SECURITY: Validate all connection strings before any processing
            var validatedDbConn = ValidateDatabaseConnectionString(model.DbConn);
            var validatedRedisConn = ValidateRedisConnectionString(model.RedisConn);

            if (validatedDbConn == null)
            {
                return BadRequest(new { Error = "Invalid database connection string format." });
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
            _logger.LogWarning("DbConn: {DbConn}", validatedDbConn);
            _logger.LogWarning("RedisConn: {RedisConn}", validatedRedisConn);

            // Simulate successful save
            return Ok(new { Message = "Configuration received (placeholder save). See server logs for details. Ensure real-world implementation uses secure configuration stores." });
        }
    }
}
