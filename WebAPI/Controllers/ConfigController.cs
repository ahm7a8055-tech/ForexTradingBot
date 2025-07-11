using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Npgsql;
using StackExchange.Redis;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Shared.Security; // For SecureExceptionSanitizer

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

        public ConfigController(
            IConfiguration configuration,
            ILogger<ConfigController> logger,
            IHttpClientFactory httpClientFactory)
            // IDiagnosticsService diagnosticsService) // Option
        {
            _configuration = configuration;
            _logger = logger;
            _httpClientFactory = httpClientFactory;
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
            sanitized = System.Text.RegularExpressions.Regex.Replace(sanitized, @"[\x00-\x1F\x7F]", "");

            // Limit length to prevent log flooding
            if (sanitized.Length > 200)
            {
                sanitized = sanitized[..200] + "...";
            }

            return sanitized;
        }

        /// <summary>
        /// Sanitizes sensitive data by redacting sensitive patterns while preserving structure.
        /// </summary>
        /// <param name="sensitiveInput">The sensitive input to sanitize</param>
        /// <returns>Sanitized string with sensitive data redacted</returns>
        private static string SanitizeSensitiveData(string? sensitiveInput)
        {
            if (string.IsNullOrWhiteSpace(sensitiveInput))
                return "[EMPTY_INPUT]";

            var sanitized = sensitiveInput;

            // Redact connection string patterns
            sanitized = System.Text.RegularExpressions.Regex.Replace(sanitized, 
                @"(?:password|pwd)\s*=\s*[^;\s]+", "password=***", 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            
            sanitized = System.Text.RegularExpressions.Regex.Replace(sanitized, 
                @"(?:user\s*id|uid|username)\s*=\s*[^;\s]+", "userid=***", 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            // Redact bot token patterns
            sanitized = System.Text.RegularExpressions.Regex.Replace(sanitized, 
                @"[0-9]+:[a-zA-Z0-9\-_]{35}", "***:***", 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            // Redact API keys and tokens
            sanitized = System.Text.RegularExpressions.Regex.Replace(sanitized, 
                @"[a-zA-Z0-9\-_]{20,}", "***", 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            return sanitized;
        }

        /// <summary>
        /// Creates a secure error response that doesn't expose sensitive information.
        /// </summary>
        /// <param name="statusCode">HTTP status code</param>
        /// <param name="userMessage">User-friendly message</param>
        /// <param name="internalErrorId">Optional internal error ID for tracking</param>
        /// <returns>Secure error response</returns>
        private IActionResult CreateSecureErrorResponse(int statusCode, string userMessage, string? internalErrorId = null)
        {
            var response = new
            {
                Message = userMessage,
                ErrorId = internalErrorId ?? Guid.NewGuid().ToString("N")[..8],
                Timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
            };

            return StatusCode(statusCode, response);
        }
        #endregion

        #region Request/Response Models
        public class TestConfigRequestModel
        {
            [Required]
            public string DbConn { get; set; } = string.Empty;

            [Required]
            public string BotToken { get; set; } = string.Empty;

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
            public string DbConn { get; set; } = string.Empty;

            [Required]
            public string BotToken { get; set; } = string.Empty;

            public string? RedisConn { get; set; }
        }
        #endregion

        #region Validation Methods
        /// <summary>
        /// Validates and sanitizes database connection string.
        /// </summary>
        /// <param name="connectionString">The connection string to validate</param>
        /// <returns>Validated connection string or null if invalid</returns>
        private string? ValidateDatabaseConnectionString(string? connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                return null;

            // SECURITY: Sanitize input before any processing
            var sanitizedInput = SanitizeForLogging(connectionString);
            
            try
            {
                // Basic validation - check for required components
                if (!connectionString.Contains("Server=", StringComparison.OrdinalIgnoreCase) &&
                    !connectionString.Contains("Host=", StringComparison.OrdinalIgnoreCase) &&
                    !connectionString.Contains("Data Source=", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("Database connection string validation failed: Missing server/host information. Input: {SanitizedInput}", sanitizedInput);
                    return null;
                }

                if (!connectionString.Contains("Database=", StringComparison.OrdinalIgnoreCase) &&
                    !connectionString.Contains("Initial Catalog=", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("Database connection string validation failed: Missing database information. Input: {SanitizedInput}", sanitizedInput);
                    return null;
                }

                // Additional validation for PostgreSQL
                if (connectionString.Contains("PostgreSQL", StringComparison.OrdinalIgnoreCase) ||
                    connectionString.Contains("postgres", StringComparison.OrdinalIgnoreCase))
                {
                    if (!connectionString.Contains("Username=", StringComparison.OrdinalIgnoreCase) &&
                        !connectionString.Contains("User Id=", StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogWarning("PostgreSQL connection string validation failed: Missing username. Input: {SanitizedInput}", sanitizedInput);
                        return null;
                    }
                }

                _logger.LogInformation("Database connection string validation successful. Input: {SanitizedInput}", sanitizedInput);
                return connectionString;
            }
            catch (Exception ex)
            {
                // SECURITY: Use SecureExceptionSanitizer for logging exceptions
                var sanitizedException = SecureExceptionSanitizer.SanitizeForLogging(ex);
                _logger.LogError(sanitizedException, "Database connection string validation failed. Input: {SanitizedInput}", sanitizedInput);
                return null;
            }
        }

        /// <summary>
        /// Validates and sanitizes Redis connection string.
        /// </summary>
        /// <param name="connectionString">The connection string to validate</param>
        /// <returns>Validated connection string or null if invalid</returns>
        private string? ValidateRedisConnectionString(string? connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                return null;

            // SECURITY: Sanitize input before any processing
            var sanitizedInput = SanitizeForLogging(connectionString);
            
            try
            {
                // Basic Redis connection string validation
                if (!connectionString.Contains(":", StringComparison.OrdinalIgnoreCase) &&
                    !connectionString.Contains("localhost", StringComparison.OrdinalIgnoreCase) &&
                    !connectionString.Contains("127.0.0.1", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("Redis connection string validation failed: Invalid format. Input: {SanitizedInput}", sanitizedInput);
                    return null;
                }

                _logger.LogInformation("Redis connection string validation successful. Input: {SanitizedInput}", sanitizedInput);
                return connectionString;
            }
            catch (Exception ex)
            {
                // SECURITY: Use SecureExceptionSanitizer for logging exceptions
                var sanitizedException = SecureExceptionSanitizer.SanitizeForLogging(ex);
                _logger.LogError(sanitizedException, "Redis connection string validation failed. Input: {SanitizedInput}", sanitizedInput);
                return null;
            }
        }
        #endregion

        #region API Endpoints
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
                var errorId = Guid.NewGuid().ToString("N")[..8];
                _logger.LogError(sanitizedException, "Database connection test failed. ErrorId: {ErrorId}", errorId);
                response.DatabaseStatus = "Error";
                response.DatabaseError = "Database connection failed. Please check your connection string and network connectivity.";
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
                    var errorId = Guid.NewGuid().ToString("N")[..8];
                    _logger.LogError(sanitizedException, "Redis connection test failed. ErrorId: {ErrorId}", errorId);
                    response.RedisStatus = "Error";
                    response.RedisError = "Redis connection failed. Please check your connection string and network connectivity.";
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
                        var errorId = Guid.NewGuid().ToString("N")[..8];
                        _logger.LogWarning("Telegram Bot Token test failed. Status: {StatusCode}, Response: {SanitizedErrorContent}, ErrorId: {ErrorId}", 
                            telegramApiResponse.StatusCode, sanitizedErrorContent, errorId);
                        response.TelegramStatus = "Error";
                        response.TelegramError = "Telegram API test failed. Please check your bot token.";
                    }
                }
            }
            catch (Exception ex)
            {
                // SECURITY: Use SecureExceptionSanitizer for logging exceptions
                var sanitizedException = SecureExceptionSanitizer.SanitizeForLogging(ex);
                var errorId = Guid.NewGuid().ToString("N")[..8];
                _logger.LogError(sanitizedException, "Telegram Bot Token test failed. ErrorId: {ErrorId}", errorId);
                response.TelegramStatus = "Error";
                response.TelegramError = "Telegram bot token test failed. Please check your token and network connectivity.";
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
                return CreateSecureErrorResponse(
                    StatusCodes.Status400BadRequest, 
                    "Invalid database connection string format.");
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

            // SECURITY: Return a secure response without exposing sensitive information
            return Ok(new { 
                Message = "Configuration received successfully. This is a placeholder implementation. In production, use secure configuration stores.",
                Status = "Success",
                Timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
            });
        }
        #endregion
    }
}
