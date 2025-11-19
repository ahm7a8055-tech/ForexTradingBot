namespace Shared.Security
{
    /// <summary>
    /// Static wrapper for ExceptionSanitizer providing convenient access to sanitization methods.
    /// This maintains backward compatibility while using the new interface-based implementation.
    /// </summary>
    public static class SecureExceptionSanitizer
    {
        private static readonly IExceptionSanitizer _sanitizer = new ExceptionSanitizer();

        #region Static Methods for Backward Compatibility
        /// <summary>
        /// Sanitizes exception details with basic redaction of sensitive patterns.
        /// </summary>
        /// <param name="exception">The exception to sanitize</param>
        /// <returns>Sanitized exception string</returns>
        public static string Sanitize(Exception exception)
        {
            return _sanitizer.Sanitize(exception);
        }

        /// <summary>
        /// Sanitizes exception details with basic redaction and adds a security hash.
        /// </summary>
        /// <param name="exception">The exception to sanitize</param>
        /// <param name="includeHash">Whether to include a security hash</param>
        /// <returns>Sanitized exception string with optional hash</returns>
        public static string Sanitize(Exception exception, bool includeHash)
        {
            return _sanitizer.Sanitize(exception, includeHash);
        }

        /// <summary>
        /// Sanitizes exception details with encryption for highly sensitive data.
        /// </summary>
        /// <param name="exception">The exception to sanitize</param>
        /// <param name="encryptionKey">Optional encryption key (uses default if not provided)</param>
        /// <returns>Sanitized and encrypted exception string</returns>
        public static string SanitizeWithEncryption(Exception exception, string? encryptionKey = null)
        {
            return _sanitizer.SanitizeWithEncryption(exception, encryptionKey);
        }

        /// <summary>
        /// Decrypts an encrypted exception string.
        /// </summary>
        /// <param name="encryptedString">The encrypted string to decrypt</param>
        /// <param name="encryptionKey">The encryption key used</param>
        /// <returns>Decrypted exception string</returns>
        public static string DecryptException(string encryptedString, string? encryptionKey = null)
        {
            return _sanitizer.DecryptException(encryptedString, encryptionKey);
        }
        #endregion

        #region Convenience Methods for Common Use Cases
        /// <summary>
        /// Sanitizes exception for logging with security hash (recommended for production logs).
        /// </summary>
        /// <param name="exception">The exception to sanitize</param>
        /// <returns>Sanitized exception string with security hash</returns>
        public static string SanitizeForLogging(Exception exception)
        {
            // SECURITY: Encrypt sanitized exception for logging. Replace with secure key management in production.
            const string encryptionKey = "YourSecureEncryptionKey"; // TODO: Use a secure key vault or environment variable
            return _sanitizer.SanitizeWithEncryption(exception, encryptionKey);
        }

        /// <summary>
        /// 🚀 ENHANCED: Sanitizes exception for Telegram admin notifications with detailed analysis.
        /// This version provides comprehensive error details while protecting sensitive data.
        /// </summary>
        /// <param name="exception">The exception to sanitize</param>
        /// <returns>Enhanced sanitized exception string with detailed analysis</returns>
        public static string SanitizeForTelegram(Exception exception)
        {
            return _sanitizer.Sanitize(exception, includeHash: true);
        }

        /// <summary>
        /// 🆕 NEW: Sanitizes exception for Telegram with maximum detail while maintaining security.
        /// This provides the most detailed error information possible for debugging.
        /// </summary>
        /// <param name="exception">The exception to sanitize</param>
        /// <returns>Maximum detail sanitized exception string for Telegram</returns>
        public static string SanitizeForTelegramDetailed(Exception exception)
        {
            return _sanitizer.Sanitize(exception, includeHash: true);
        }

        /// <summary>
        /// Sanitizes exception for database storage (encrypted for audit trail).
        /// </summary>
        /// <param name="exception">The exception to sanitize</param>
        /// <param name="encryptionKey">Optional encryption key</param>
        /// <returns>Sanitized and encrypted exception string</returns>
        public static string SanitizeForDatabase(Exception exception, string? encryptionKey = null)
        {
            return _sanitizer.SanitizeWithEncryption(exception, encryptionKey);
        }
        #endregion
    }
}