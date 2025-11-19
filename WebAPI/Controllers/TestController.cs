using Microsoft.AspNetCore.Mvc;
using Shared.Security;

namespace WebAPI.Controllers
{
    [ApiController]
    [Route("api/test")]
    public class TestController : ControllerBase
    {
        private readonly ILogger<TestController> _logger;

        public TestController(ILogger<TestController> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Test endpoint to demonstrate enhanced error reporting with different exception types
        /// </summary>
        [HttpGet("error/{type}")]
        public IActionResult TestError(string type)
        {
            try
            {
                switch (type.ToLowerInvariant())
                {
                    case "database":
                        throw new InvalidOperationException("Cannot connect to database server 'localhost:5432'. Connection timeout after 30 seconds. User: admin, Database: forextradingbot");

                    case "validation":
                        throw new ArgumentException("Invalid setting key provided: '[REDACTED]'. Key must be alphanumeric and contain no special characters.");

                    case "network":
                        throw new System.Net.Http.HttpRequestException("HTTP request failed with status code 503. Endpoint: https://api.telegram.org/bot[TOKEN]/getMe");

                    case "file":
                        throw new System.IO.FileNotFoundException("Configuration file not found: /app/config/sensitive_settings.json");

                    case "timeout":
                        throw new System.TimeoutException("Redis connection timeout after 5000ms. Server: localhost:6379");

                    case "permission":
                        throw new System.UnauthorizedAccessException("Access denied to admin panel. User ID: 12345, Required Role: Admin");

                    case "memory":
                        throw new System.OutOfMemoryException("Application memory usage exceeded 2GB limit. Current usage: 2.1GB");

                    case "null":
                        throw new System.NullReferenceException("Object reference not set to an instance of an object. Variable: userSettings");

                    case "format":
                        throw new System.FormatException("Invalid JSON format in configuration. Expected: {\"key\": \"value\"}, Got: {invalid json}");

                    case "complex":
                        // Test a complex exception with inner exception
                        HttpRequestException innerEx = new("Inner network error: DNS resolution failed");
                        throw new InvalidOperationException("Complex operation failed due to network issues", innerEx);

                    default:
                        return BadRequest($"Unknown error type: {type}. Available types: database, validation, network, file, timeout, permission, memory, null, format, complex");
                }
            }
            catch (Exception ex)
            {
                // Use the enhanced sanitizer for detailed error reporting
                const string encryptionKey = "YourSecureEncryptionKey"; // TODO: Replace with a secure key management solution
                string sanitizedDetails = SecureExceptionSanitizer.SanitizeWithEncryption(ex, encryptionKey);
                string sanitizedType = SanitizeForLog(type);
                _logger.LogError(sanitizedDetails, "Test error triggered: {ErrorType}", sanitizedType);

                return Ok(new
                {
                    Message = $"Test error '{type}' triggered successfully",
                    ErrorType = ex.GetType().Name,
                    SanitizedDetails = sanitizedDetails,
                    Timestamp = DateTime.UtcNow
                });
            }
        }

        /// <summary>
        /// Test endpoint to demonstrate the old vs new error reporting
        /// </summary>
        [HttpGet("compare")]
        public IActionResult CompareErrorReporting()
        {
            try
            {
                // Simulate a realistic error that might occur
                throw new InvalidOperationException(" 'Admin:Password'. Key contains sensitive information that should not be logged.");
            }
            catch (Exception ex)
            {
                // Old way (aggressive sanitization)
                string oldSanitized = ex.ToString().Replace("Admin:Password", "[REDACTED]");

                // New way (enhanced sanitization)
                string newSanitized = SecureExceptionSanitizer.SanitizeForTelegram(ex);

                return Ok(new
                {
                    OldWay = oldSanitized,
                    NewWay = newSanitized,
                    Comparison = "The new way provides detailed error analysis while still protecting sensitive data"
                });
            }
        }

        // Add a helper method to sanitize user input for logs
        private static string SanitizeForLog(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return string.Empty;
            }
            // Remove newlines and other log-forging characters
            return input.Replace("\r", "").Replace("\n", "").Replace("|", "").Replace("\u2028", "").Replace("\u2029", "");
        }
    }
}