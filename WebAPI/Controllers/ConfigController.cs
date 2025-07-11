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
using System.Linq; // For Select
using System.Text.RegularExpressions;

namespace WebAPI.Controllers
{
    [ApiController]
    [Route("api/config")]
    [Authorize(Roles = "Admin")]
    public class ConfigController : ControllerBase
    {
        #region Fields and Constructor
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
            if (sanitized.Length > 100)
            {
                sanitized = sanitized.Substring(0, 97) + "...";
            }

            return sanitized;
        }

        /// <summary>
        /// Sanitizes sensitive data for logging by masking most of the content.
        /// </summary>
        /// <param name="sensitiveInput">The sensitive input to sanitize</param>
        /// <returns>Masked string safe for logging</returns>
        private static string SanitizeSensitiveData(string? sensitiveInput)
        {
            if (string.IsNullOrWhiteSpace(sensitiveInput))
                return "[EMPTY_SENSITIVE_INPUT]";

            // For sensitive data like tokens and connection strings, mask most of the content
            if (sensitiveInput.Length <= 8)
            {
                return "[MASKED_SENSITIVE_DATA]";
            }

            // Show first 4 and last 4 characters, mask the rest
            return $"{sensitiveInput.Substring(0, 4)}...{sensitiveInput.Substring(sensitiveInput.Length - 4)}";
        }
        #endregion

        #region DTOs
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

        public class SaveConfigRequestModel
        {
            [Required]
            public string? BotToken { get; set; }
            [Required]
            public string? DbConn { get; set; }
            public string? RedisConn { get; set; }
        }
        #endregion

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

                // SECURITY: Log only safe parts of the connection string, mask sensitive data
                var safeConnectionInfo = $"Host={SanitizeForLogging(builder.Host)}, Port={builder.Port}, Database={SanitizeForLogging(builder.Database)}";
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

                // SECURITY: Log only safe parts of the connection string, mask sensitive data
                var endpoints = string.Join(", ", options.EndPoints.Select(ep => SanitizeForLogging(ep.ToString())));
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
                        // SECURITY: Sanitize bot username before logging
                        var sanitizedBotUsername = SanitizeForLogging(response.BotUsername);
                        _logger.LogInformation("Telegram Bot Token test successful. Bot Username: {SanitizedBotUsername}", sanitizedBotUsername);
                    }
                    else
                    {
                        var errorContent = await telegramApiResponse.Content.ReadAsStringAsync();
                        // SECURITY: Sanitize error content before logging
                        var sanitizedErrorContent = SanitizeForLogging(errorContent);
                        _logger.LogWarning("Telegram Bot Token test failed. Status: {StatusCode}, Response: {SanitizedErrorContent}", 
                            telegramApiResponse.StatusCode, sanitizedErrorContent);
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
            
            // SECURITY: Sanitize all user inputs before logging to prevent log forging
            var sanitizedBotToken = SanitizeSensitiveData(model.BotToken);
            var sanitizedDbConn = SanitizeSensitiveData(validatedDbConn);
            var sanitizedRedisConn = SanitizeSensitiveData(validatedRedisConn);
            
            _logger.LogWarning("Received configuration to save (PLACEHOLDER - NOT SAVING TO APPSETTINGS.JSON):");
            _logger.LogWarning("BotToken: {SanitizedBotToken}", sanitizedBotToken);
            _logger.LogWarning("DbConn: {SanitizedDbConn}", sanitizedDbConn);
            _logger.LogWarning("RedisConn: {SanitizedRedisConn}", sanitizedRedisConn);

            // Simulate successful save
            return Ok(new { Message = "Configuration received (placeholder save). See server logs for details. Ensure real-world implementation uses secure configuration stores." });
        }
    }
}
