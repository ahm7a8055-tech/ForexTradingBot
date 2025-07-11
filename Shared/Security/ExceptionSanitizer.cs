using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Shared.Security
{
    /// <summary>
    /// Robust exception sanitizer with encryption capabilities for security-sensitive logging.
    /// This class provides multiple levels of sanitization and encryption for sensitive data.
    /// </summary>
    public class ExceptionSanitizer : IExceptionSanitizer
    {
        #region Constants and Patterns
        private static readonly string[] SensitivePatterns = {
            // Connection strings
            @"(?:connection\s*string|conn\s*str|connectionstring)\s*[=:]\s*[^;\s]+",
            @"(?:server|host|data\s*source)\s*[=:]\s*[^;\s]+",
            @"(?:database|initial\s*catalog)\s*[=:]\s*[^;\s]+",
            @"(?:user\s*id|uid|username)\s*[=:]\s*[^;\s]+",
            @"(?:password|pwd)\s*[=:]\s*[^;\s]+",
            
            // API Keys and Tokens
            @"(?:api\s*key|apikey|token|secret)\s*[=:]\s*[a-zA-Z0-9\-_]{20,}",
            @"(?:bot\s*token|telegram\s*token)\s*[=:]\s*[0-9]+:[a-zA-Z0-9\-_]{35}",
            @"(?:api\s*hash|apihash)\s*[=:]\s*[a-fA-F0-9]{32}",
            @"(?:api\s*id|apiid)\s*[=:]\s*[0-9]+",
            
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
            "[PHONE_REDACTED]",
            "[EMAIL_REDACTED]",
            "[IP_REDACTED]",
            "[FILE_PATH_REDACTED]",
            "[SESSION_ID_REDACTED]"
        };
        #endregion

        #region Public Methods
        /// <summary>
        /// Sanitizes exception details with basic redaction of sensitive patterns.
        /// </summary>
        /// <param name="exception">The exception to sanitize</param>
        /// <returns>Sanitized exception string</returns>
        public string Sanitize(Exception exception)
        {
            if (exception == null) return "[NULL_EXCEPTION]";
            
            try
            {
                string exceptionString = exception.ToString();
                return ApplyBasicSanitization(exceptionString);
            }
            catch (Exception ex)
            {
                return $"[SANITIZATION_FAILED: {ex.Message}]";
            }
        }

        /// <summary>
        /// Sanitizes exception details with basic redaction and adds a security hash.
        /// </summary>
        /// <param name="exception">The exception to sanitize</param>
        /// <param name="includeHash">Whether to include a security hash</param>
        /// <returns>Sanitized exception string with optional hash</returns>
        public string Sanitize(Exception exception, bool includeHash)
        {
            if (exception == null) return "[NULL_EXCEPTION]";
            
            try
            {
                string exceptionString = exception.ToString();
                string sanitized = ApplyBasicSanitization(exceptionString);
                
                if (includeHash)
                {
                    string hash = GenerateSecurityHash(exceptionString);
                    sanitized += $"\n[SECURITY_HASH: {hash}]";
                }
                
                return sanitized;
            }
            catch (Exception ex)
            {
                return $"[SANITIZATION_FAILED: {ex.Message}]";
            }
        }

        /// <summary>
        /// Sanitizes exception details with encryption for highly sensitive data.
        /// </summary>
        /// <param name="exception">The exception to sanitize</param>
        /// <param name="encryptionKey">Optional encryption key (uses default if not provided)</param>
        /// <returns>Sanitized and encrypted exception string</returns>
        public string SanitizeWithEncryption(Exception exception, string? encryptionKey = null)
        {
            if (exception == null) return "[NULL_EXCEPTION]";
            
            try
            {
                string exceptionString = exception.ToString();
                string sanitized = ApplyBasicSanitization(exceptionString);
                
                // Encrypt the original exception for audit purposes
                string encryptedOriginal = EncryptString(exceptionString, encryptionKey);
                string hash = GenerateSecurityHash(exceptionString);
                
                return $"{sanitized}\n[ENCRYPTED_ORIGINAL: {encryptedOriginal}]\n[SECURITY_HASH: {hash}]";
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
            if (string.IsNullOrEmpty(encryptedString)) return "[EMPTY_ENCRYPTED_STRING]";
            
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

        #region Private Methods
        private static string ApplyBasicSanitization(string input)
        {
            if (string.IsNullOrEmpty(input)) return "[EMPTY_STRING]";
            
            string sanitized = input;
            
            // Apply pattern-based redaction
            for (int i = 0; i < SensitivePatterns.Length && i < RedactedReplacements.Length; i++)
            {
                sanitized = Regex.Replace(sanitized, SensitivePatterns[i], RedactedReplacements[i], 
                    RegexOptions.IgnoreCase | RegexOptions.Multiline);
            }
            
            // Additional sanitization for common sensitive patterns
            sanitized = Regex.Replace(sanitized, @"(?:password|pwd)\s*=\s*[^;\s]+", "[PASSWORD_REDACTED]", 
                RegexOptions.IgnoreCase);
            sanitized = Regex.Replace(sanitized, @"(?:token|secret)\s*=\s*[a-zA-Z0-9\-_]{20,}", "[TOKEN_REDACTED]", 
                RegexOptions.IgnoreCase);
            
            return sanitized;
        }

        private static string GenerateSecurityHash(string input)
        {
            if (string.IsNullOrEmpty(input)) return "[EMPTY_HASH]";
            
            try
            {
                using var sha256 = SHA256.Create();
                byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
                return Convert.ToBase64String(hashBytes).Substring(0, 16); // First 16 chars for readability
            }
            catch
            {
                return "[HASH_GENERATION_FAILED]";
            }
        }

        private static string EncryptString(string plainText, string? key = null)
        {
            if (string.IsNullOrEmpty(plainText)) return "[EMPTY_PLAINTEXT]";
            
            try
            {
                string encryptionKey = key ?? GetDefaultEncryptionKey();
                
                using var aes = Aes.Create();
                aes.Key = Encoding.UTF8.GetBytes(encryptionKey.PadRight(32).Substring(0, 32));
                aes.IV = new byte[16]; // Zero IV for simplicity (use random IV in production)
                
                using var encryptor = aes.CreateEncryptor();
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
            if (string.IsNullOrEmpty(encryptedText)) return "[EMPTY_ENCRYPTED_TEXT]";
            
            try
            {
                string encryptionKey = key ?? GetDefaultEncryptionKey();
                
                using var aes = Aes.Create();
                aes.Key = Encoding.UTF8.GetBytes(encryptionKey.PadRight(32).Substring(0, 32));
                aes.IV = new byte[16]; // Zero IV for simplicity
                
                using var decryptor = aes.CreateDecryptor();
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