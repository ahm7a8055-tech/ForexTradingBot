namespace Shared.Security
{
    /// <summary>
    /// Interface for exception sanitization with encryption capabilities.
    /// Follows clean architecture principles for dependency injection.
    /// </summary>
    public interface IExceptionSanitizer
    {
        /// <summary>
        /// Sanitizes exception details with basic redaction of sensitive patterns.
        /// </summary>
        /// <param name="exception">The exception to sanitize</param>
        /// <returns>Sanitized exception string</returns>
        string Sanitize(Exception exception);

        /// <summary>
        /// Sanitizes exception details with basic redaction and adds a security hash.
        /// </summary>
        /// <param name="exception">The exception to sanitize</param>
        /// <param name="includeHash">Whether to include a security hash</param>
        /// <returns>Sanitized exception string with optional hash</returns>
        string Sanitize(Exception exception, bool includeHash);

        /// <summary>
        /// Sanitizes exception details with encryption for highly sensitive data.
        /// </summary>
        /// <param name="exception">The exception to sanitize</param>
        /// <param name="encryptionKey">Optional encryption key (uses default if not provided)</param>
        /// <returns>Sanitized and encrypted exception string</returns>
        string SanitizeWithEncryption(Exception exception, string? encryptionKey = null);

        /// <summary>
        /// Decrypts an encrypted exception string.
        /// </summary>
        /// <param name="encryptedString">The encrypted string to decrypt</param>
        /// <param name="encryptionKey">The encryption key used</param>
        /// <returns>Decrypted exception string</returns>
        string DecryptException(string encryptedString, string? encryptionKey = null);
    }
}