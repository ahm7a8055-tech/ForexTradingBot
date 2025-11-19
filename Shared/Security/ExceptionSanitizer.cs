using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Shared.Security
{
    /// <summary>
    /// 🚀 POWERFUL UPGRADE: Enhanced exception sanitizer with intelligent error analysis,
    /// detailed debugging information, and smart categorization while maintaining security.
    /// This version preserves important error details while protecting sensitive data.
    /// </summary>
    public class ExceptionSanitizer : IExceptionSanitizer
    {
        #region Enhanced Constants and Patterns
        private static readonly string[] SensitivePatterns = {
            // Connection strings - more specific patterns
            @"(?:connection\s*string|conn\s*str|connectionstring)\s*[=:]\s*[^;\s]+",
            @"(?:server|host|data\s*source)\s*[=:]\s*[^;\s]+",
            @"(?:database|initial\s*catalog)\s*[=:]\s*[^;\s]+",
            @"(?:user\s*id|uid|username)\s*[=:]\s*[^;\s]+",
            @"(?:password|pwd)\s*[=:]\s*[^;\s]+",
            
            // API Keys and Tokens - more precise matching
            @"(?:api\s*key|apikey)\s*[=:]\s*[a-zA-Z0-9\-_]{20,}",
            @"(?:bot\s*token|telegram\s*token)\s*[=:]\s*[0-9]+:[a-zA-Z0-9\-_]{35}",
            @"(?:api\s*hash|apihash)\s*[=:]\s*[a-fA-F0-9]{32}",
            @"(?:api\s*id|apiid)\s*[=:]\s*[0-9]+",
            @"(?:secret|token)\s*[=:]\s*[a-zA-Z0-9\-_]{20,}",
            
            // Phone numbers
            @"(?:phone|mobile|tel)\s*[=:]\s*[\+]?[0-9\s\-\(\)]{10,}",
            
            // Email addresses
            @"(?:email|mail)\s*[=:]\s*[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}",
            
            // IP addresses
            @"(?:ip|address)\s*[=:]\s*\b(?:[0-9]{1,3}\.){3}[0-9]{1,3}\b",
            
            // File paths with sensitive names
            @"(?:path|file)\s*[=:]\s*[^;\s]*(?:password|secret|key|token)[^;\s]*",
            
            // Session IDs and GUIDs that might be sensitive
            @"(?:session|guid|id)\s*[=:]\s*[a-fA-F0-9\-]{20,}"
        };

        private static readonly string[] RedactedReplacements = {
            "[CONNECTION_STRING_REDACTED]",
            "[SERVER_REDACTED]",
            "[DATABASE_REDACTED]",
            "[USERNAME_REDACTED]",
            "[PASSWORD_REDACTED]",
            "[API_KEY_REDACTED]",
            "[BOT_TOKEN_REDACTED]",
            "[API_HASH_REDACTED]",
            "[API_ID_REDACTED]",
            "[SECRET_REDACTED]",
            "[PHONE_REDACTED]",
            "[EMAIL_REDACTED]",
            "[IP_REDACTED]",
            "[FILE_PATH_REDACTED]",
            "[SESSION_ID_REDACTED]"
        };

        // 🆕 NEW: Error categorization patterns
        private static readonly Dictionary<string, string> ErrorCategories = new()
        {
            // Database errors
            { "SqlException", "DATABASE_ERROR" },
            { "NpgsqlException", "DATABASE_ERROR" },
            { "DbUpdateException", "DATABASE_ERROR" },
            { "InvalidOperationException", "OPERATION_ERROR" },
            { "ArgumentException", "VALIDATION_ERROR" },
            { "ArgumentNullException", "VALIDATION_ERROR" },
            { "HttpRequestException", "NETWORK_ERROR" },
            { "TimeoutException", "TIMEOUT_ERROR" },
            { "UnauthorizedAccessException", "AUTHORIZATION_ERROR" },
            { "FileNotFoundException", "FILE_ERROR" },
            { "DirectoryNotFoundException", "FILE_ERROR" },
            { "IOException", "IO_ERROR" },
            { "JsonException", "DATA_ERROR" },
            { "FormatException", "DATA_ERROR" },
            { "NotSupportedException", "COMPATIBILITY_ERROR" },
            { "OutOfMemoryException", "RESOURCE_ERROR" },
            { "StackOverflowException", "RESOURCE_ERROR" }
        };

        // 🆕 NEW: Error severity levels
        private static readonly Dictionary<string, string> ErrorSeverity = new()
        {
            { "DATABASE_ERROR", "🔴 CRITICAL" },
            { "AUTHORIZATION_ERROR", "🔴 CRITICAL" },
            { "NETWORK_ERROR", "🟡 WARNING" },
            { "TIMEOUT_ERROR", "🟡 WARNING" },
            { "VALIDATION_ERROR", "🟠 ERROR" },
            { "OPERATION_ERROR", "🟠 ERROR" },
            { "FILE_ERROR", "🟠 ERROR" },
            { "IO_ERROR", "🟠 ERROR" },
            { "DATA_ERROR", "🟠 ERROR" },
            { "COMPATIBILITY_ERROR", "🔵 INFO" },
            { "RESOURCE_ERROR", "🔴 CRITICAL" }
        };

        // 🆕 NEW: Intelligent suggestions
        private static readonly Dictionary<string, string[]> ErrorSuggestions = new()
        {
            { "DATABASE_ERROR", new[] {
                "🔍 Check database connection string and server status",
                "🔐 Verify database user permissions and credentials",
                "📊 Monitor database performance and connection pool",
                "🔄 Ensure database schema is up to date"
            }},
            { "NETWORK_ERROR", new[] {
                "🌐 Check network connectivity and firewall settings",
                "🔗 Verify endpoint URLs and DNS resolution",
                "⏱️ Check for timeout configurations",
                "🔄 Retry with exponential backoff"
            }},
            { "AUTHORIZATION_ERROR", new[] {
                "🔐 Verify authentication tokens and API keys",
                "👤 Check user permissions and roles",
                "🔑 Ensure proper authorization headers",
                "📋 Review access control policies"
            }},
            { "VALIDATION_ERROR", new[] {
                "✅ Validate input data format and constraints",
                "📝 Check required fields and data types",
                "🔍 Review business logic validation rules",
                "📋 Ensure proper error handling for invalid inputs"
            }}
        };
        #endregion

        #region Enhanced Public Methods
        /// <summary>
        /// 🚀 ENHANCED: Sanitizes exception with intelligent analysis and detailed debugging info.
        /// </summary>
        /// <param name="exception">The exception to sanitize</param>
        /// <returns>Enhanced sanitized exception string with analysis</returns>
        public string Sanitize(Exception exception)
        {
            if (exception == null)
            {
                return "[NULL_EXCEPTION]";
            }

            try
            {
                return BuildEnhancedExceptionReport(exception, includeHash: false, includeEncryption: false);
            }
            catch (Exception ex)
            {
                return $"[SANITIZATION_FAILED: {ex.Message}]";
            }
        }

        /// <summary>
        /// 🚀 ENHANCED: Sanitizes exception with security hash and intelligent analysis.
        /// </summary>
        /// <param name="exception">The exception to sanitize</param>
        /// <param name="includeHash">Whether to include a security hash</param>
        /// <returns>Enhanced sanitized exception string with hash and analysis</returns>
        public string Sanitize(Exception exception, bool includeHash)
        {
            if (exception == null)
            {
                return "[NULL_EXCEPTION]";
            }

            try
            {
                return BuildEnhancedExceptionReport(exception, includeHash, includeEncryption: false);
            }
            catch (Exception ex)
            {
                return $"[SANITIZATION_FAILED: {ex.Message}]";
            }
        }

        /// <summary>
        /// 🚀 ENHANCED: Sanitizes exception with encryption and comprehensive analysis.
        /// </summary>
        /// <param name="exception">The exception to sanitize</param>
        /// <param name="encryptionKey">Optional encryption key</param>
        /// <returns>Enhanced sanitized and encrypted exception string</returns>
        public string SanitizeWithEncryption(Exception exception, string? encryptionKey = null)
        {
            if (exception == null)
            {
                return "[NULL_EXCEPTION]";
            }

            try
            {
                return BuildEnhancedExceptionReport(exception, includeHash: true, includeEncryption: true, encryptionKey);
            }
            catch (Exception ex)
            {
                return $"[ENCRYPTION_FAILED: {ex.Message}]";
            }
        }

        /// <summary>
        /// Decrypts an encrypted exception string.
        /// </summary>
        /// <param name="encryptedString">The encrypted string to decrypt</param>
        /// <param name="encryptionKey">The encryption key used</param>
        /// <returns>Decrypted exception string</returns>
        public string DecryptException(string encryptedString, string? encryptionKey = null)
        {
            if (string.IsNullOrEmpty(encryptedString))
            {
                return "[EMPTY_ENCRYPTED_STRING]";
            }

            try
            {
                return DecryptString(encryptedString, encryptionKey);
            }
            catch (Exception ex)
            {
                return $"[DECRYPTION_FAILED: {ex.Message}]";
            }
        }
        #endregion

        #region 🆕 NEW: Enhanced Private Methods
        private string BuildEnhancedExceptionReport(Exception exception, bool includeHash, bool includeEncryption, string? encryptionKey = null)
        {
            StringBuilder sb = new();

            // 🆕 NEW: Exception Header with Category and Severity
            string exceptionType = exception.GetType().Name;
            string category = GetErrorCategory(exceptionType);
            string severity = GetErrorSeverity(category);

            _ = sb.AppendLine($"🚨 {severity} | {category}");
            _ = sb.AppendLine($"💥 Exception Type: {exceptionType}");
            _ = sb.AppendLine($"🔢 Error Code: {GetErrorCode(exception)}");
            _ = sb.AppendLine();

            // 🆕 NEW: Enhanced Message Section
            _ = sb.AppendLine("📄 MESSAGE");
            string sanitizedMessage = ApplySmartSanitization(exception.Message);
            _ = sb.AppendLine($"`{sanitizedMessage}`");
            _ = sb.AppendLine();

            // 🆕 NEW: Stack Trace Analysis
            if (exception.StackTrace != null)
            {
                _ = sb.AppendLine("🗺️ STACK TRACE ANALYSIS");
                (string? PrimaryLocation, string? MethodName, string? FileName, int LineNumber) = AnalyzeStackTrace(exception.StackTrace);
                _ = sb.AppendLine($"📍 Primary Location: {PrimaryLocation}");
                _ = sb.AppendLine($"🔧 Method: {MethodName}");
                _ = sb.AppendLine($"📂 File: {FileName}");
                _ = sb.AppendLine($"#️⃣ Line: {LineNumber}");
                _ = sb.AppendLine();
            }

            // 🆕 NEW: Intelligent Analysis and Suggestions
            _ = sb.AppendLine("🤖 INTELLIGENT ANALYSIS");
            string[] suggestions = GetErrorSuggestions(category, exception);
            foreach (string suggestion in suggestions)
            {
                _ = sb.AppendLine($"- {suggestion}");
            }
            _ = sb.AppendLine();

            // 🆕 NEW: Inner Exception Analysis
            if (exception.InnerException != null)
            {
                _ = sb.AppendLine("🔗 INNER EXCEPTION");
                string innerType = exception.InnerException.GetType().Name;
                string innerCategory = GetErrorCategory(innerType);
                _ = sb.AppendLine($"Type: {innerType} ({innerCategory})");
                _ = sb.AppendLine($"Message: `{ApplySmartSanitization(exception.InnerException.Message)}`");
                _ = sb.AppendLine();
            }

            // 🆕 NEW: Context Information
            _ = sb.AppendLine("📊 CONTEXT INFO");
            _ = sb.AppendLine($"Timestamp: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff} UTC");
            _ = sb.AppendLine($"Environment: {Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Unknown"}");
            _ = sb.AppendLine($"Machine: {Environment.MachineName}");
            _ = sb.AppendLine($"Process ID: {Environment.ProcessId}");
            _ = sb.AppendLine();

            // Security features
            if (includeHash)
            {
                string hash = GenerateSecurityHash(exception.ToString());
                _ = sb.AppendLine($"🔐 Security Hash: {hash}");
            }

            if (includeEncryption)
            {
                string encryptedOriginal = EncryptString(exception.ToString(), encryptionKey);
                string hash = GenerateSecurityHash(exception.ToString());
                _ = sb.AppendLine($"🔐 Security Hash: {hash}");
                _ = sb.AppendLine($"🔒 Encrypted Original: {encryptedOriginal}");
            }

            return sb.ToString();
        }

        private string ApplySmartSanitization(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return "[EMPTY_MESSAGE]";
            }

            string sanitized = input;

            // Apply pattern-based redaction only for truly sensitive data
            for (int i = 0; i < SensitivePatterns.Length && i < RedactedReplacements.Length; i++)
            {
                sanitized = Regex.Replace(sanitized, SensitivePatterns[i], RedactedReplacements[i],
                    RegexOptions.IgnoreCase | RegexOptions.Multiline);
            }

            // 🆕 NEW: Preserve important error details while redacting sensitive parts
            // Only redact specific sensitive patterns, not entire messages
            sanitized = Regex.Replace(sanitized, @"(?:password|pwd)\s*=\s*[^;\s]+", "[PASSWORD_REDACTED]",
                RegexOptions.IgnoreCase);
            sanitized = Regex.Replace(sanitized, @"(?:token|secret)\s*=\s*[a-zA-Z0-9\-_]{20,}", "[TOKEN_REDACTED]",
                RegexOptions.IgnoreCase);

            return sanitized;
        }

        private string GetErrorCategory(string exceptionType)
        {
            return ErrorCategories.TryGetValue(exceptionType, out string? category) ? category : "UNKNOWN_ERROR";
        }

        private string GetErrorSeverity(string category)
        {
            return ErrorSeverity.TryGetValue(category, out string? severity) ? severity : "🔵 INFO";
        }

        private string GetErrorCode(Exception exception)
        {
            // 🆕 NEW: Extract meaningful error codes
            return exception is System.Data.SqlClient.SqlException sqlEx
                ? $"SQL-{sqlEx.Number}"
                : exception is Npgsql.PostgresException pgEx
                ? $"PG-{pgEx.SqlState}"
                : exception is System.Net.Http.HttpRequestException httpEx
                ? $"HTTP-{GetHttpStatusCode(httpEx)}"
                : exception is System.ComponentModel.Win32Exception win32Ex
                ? $"WIN32-{win32Ex.NativeErrorCode}"
                : $"GEN-{Math.Abs(exception.GetHashCode() % 10000):D4}";
        }

        private int GetHttpStatusCode(System.Net.Http.HttpRequestException httpEx)
        {
            // Try to extract HTTP status code from the exception
            Match statusMatch = Regex.Match(httpEx.Message, @"(\d{3})");
            return statusMatch.Success ? int.Parse(statusMatch.Groups[1].Value) : 0;
        }

        private (string PrimaryLocation, string MethodName, string FileName, int LineNumber) AnalyzeStackTrace(string stackTrace)
        {
            try
            {
                string[] lines = stackTrace.Split('\n', StringSplitOptions.RemoveEmptyEntries);

                // Find the first non-framework line
                foreach (string line in lines)
                {
                    Match match = Regex.Match(line.Trim(), @"at\s+(.+)\s+in\s+(.+):line\s+(\d+)");
                    if (match.Success)
                    {
                        string methodName = match.Groups[1].Value;
                        string filePath = match.Groups[2].Value;
                        int lineNumber = int.Parse(match.Groups[3].Value);
                        string fileName = System.IO.Path.GetFileName(filePath);

                        // Skip framework and system methods
                        if (!IsFrameworkMethod(methodName))
                        {
                            return (filePath, methodName, fileName, lineNumber);
                        }
                    }
                }

                // Fallback to first line
                Match fallbackMatch = Regex.Match(lines.FirstOrDefault() ?? "", @"at\s+(.+)\s+in\s+(.+):line\s+(\d+)");
                if (fallbackMatch.Success)
                {
                    return (
                        fallbackMatch.Groups[2].Value,
                        fallbackMatch.Groups[1].Value,
                        System.IO.Path.GetFileName(fallbackMatch.Groups[2].Value),
                        int.Parse(fallbackMatch.Groups[3].Value)
                    );
                }
            }
            catch { }

            return ("Unknown", "Unknown", "Unknown", 0);
        }

        private bool IsFrameworkMethod(string methodName)
        {
            string[] frameworkNames = new[] { "System.", "Microsoft.", "mscorlib", "netstandard" };
            return frameworkNames.Any(framework => methodName.StartsWith(framework, StringComparison.OrdinalIgnoreCase));
        }

        private string[] GetErrorSuggestions(string category, Exception exception)
        {
            if (ErrorSuggestions.TryGetValue(category, out string[]? suggestions))
            {
                return suggestions;
            }

            // 🆕 NEW: Dynamic suggestions based on exception content
            List<string> dynamicSuggestions = [];

            if (exception.Message.Contains("connection", StringComparison.OrdinalIgnoreCase))
            {
                dynamicSuggestions.Add("🔍 Check network connectivity and firewall settings");
                dynamicSuggestions.Add("🔗 Verify connection strings and endpoints");
            }

            if (exception.Message.Contains("permission", StringComparison.OrdinalIgnoreCase))
            {
                dynamicSuggestions.Add("🔐 Verify user permissions and access rights");
                dynamicSuggestions.Add("👤 Check authentication and authorization");
            }

            if (exception.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase))
            {
                dynamicSuggestions.Add("⏱️ Increase timeout values or optimize performance");
                dynamicSuggestions.Add("🔄 Implement retry logic with exponential backoff");
            }

            if (dynamicSuggestions.Count == 0)
            {
                dynamicSuggestions.Add("🔍 Review the stack trace for specific error details");
                dynamicSuggestions.Add("📋 Check application logs for additional context");
                dynamicSuggestions.Add("🔄 Consider restarting the affected service");
            }

            return dynamicSuggestions.ToArray();
        }

        private static string GenerateSecurityHash(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return "[EMPTY_HASH]";
            }

            try
            {
                using SHA256 sha256 = SHA256.Create();
                byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
                return Convert.ToBase64String(hashBytes)[..16]; // First 16 chars for readability
            }
            catch
            {
                return "[HASH_GENERATION_FAILED]";
            }
        }

        private static string EncryptString(string plainText, string? key = null)
        {
            if (string.IsNullOrEmpty(plainText))
            {
                return "[EMPTY_PLAINTEXT]";
            }

            try
            {
                string encryptionKey = key ?? GetDefaultEncryptionKey();

                using Aes aes = Aes.Create();
                aes.Key = Encoding.UTF8.GetBytes(encryptionKey.PadRight(32)[..32]);
                aes.IV = new byte[16]; // Zero IV for simplicity (use random IV in production)

                using ICryptoTransform encryptor = aes.CreateEncryptor();
                byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
                byte[] encryptedBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

                return Convert.ToBase64String(encryptedBytes);
            }
            catch (Exception ex)
            {
                return $"[ENCRYPTION_ERROR: {ex.Message}]";
            }
        }

        private static string DecryptString(string encryptedText, string? key = null)
        {
            if (string.IsNullOrEmpty(encryptedText))
            {
                return "[EMPTY_ENCRYPTED_TEXT]";
            }

            try
            {
                string encryptionKey = key ?? GetDefaultEncryptionKey();

                using Aes aes = Aes.Create();
                aes.Key = Encoding.UTF8.GetBytes(encryptionKey.PadRight(32)[..32]);
                aes.IV = new byte[16]; // Zero IV for simplicity

                using ICryptoTransform decryptor = aes.CreateDecryptor();
                byte[] encryptedBytes = Convert.FromBase64String(encryptedText);
                byte[] decryptedBytes = decryptor.TransformFinalBlock(encryptedBytes, 0, encryptedBytes.Length);

                return Encoding.UTF8.GetString(decryptedBytes);
            }
            catch (Exception ex)
            {
                return $"[DECRYPTION_ERROR: {ex.Message}]";
            }
        }

        private static string GetDefaultEncryptionKey()
        {
            // In production, this should come from secure configuration
            // For now, using a hardcoded key (NOT recommended for production)
            return "ForexTradingBot2024SecureKey!";
        }
        #endregion
    }
}