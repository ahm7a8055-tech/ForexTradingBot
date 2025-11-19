using Application.Common.Interfaces;
using Microsoft.AspNetCore.DataProtection;

namespace Infrastructure.Security
{
    public class SettingsProtectionService : ISettingsProtectionService
    {
        private readonly IDataProtector _dataProtector;
        private const string ProtectorPurpose = "ApplicationSettings.SensitiveValues.v1";

        public SettingsProtectionService(IDataProtectionProvider dataProtectionProvider)
        {
            if (dataProtectionProvider == null)
            {
                throw new ArgumentNullException(nameof(dataProtectionProvider));
            }
            // Create a protector with a specific purpose string.
            // This ensures that data protected with this purpose cannot be unprotected by code using a different purpose.
            _dataProtector = dataProtectionProvider.CreateProtector(ProtectorPurpose);
        }

        public string Encrypt(string plaintext)
        {
            if (plaintext == null) // Allow encrypting null as null or empty string as empty string
            {
                // Depending on requirements, you might want to throw ArgumentNullException
                // or handle null/empty strings differently. For settings, an empty string is a valid value.
                return string.Empty;
            }
            return _dataProtector.Protect(plaintext);
        }

        public string Decrypt(string ciphertext)
        {
            if (string.IsNullOrEmpty(ciphertext)) // If it's null or empty, it wasn't encrypted in the first place by this service's Encrypt method
            {
                return string.Empty;
            }
            try
            {
                return _dataProtector.Unprotect(ciphertext);
            }
            catch (System.Security.Cryptography.CryptographicException ex)
            {
                // Log this exception in a real application
                // Consider the implications: if a setting can't be decrypted, what should the app do?
                // Throwing here will likely prevent the app from using the corrupted setting.
                throw new InvalidOperationException($"Failed to decrypt setting. The data may be corrupted or the protection keys may have changed. Purpose: {ProtectorPurpose}", ex);
            }
        }
    }
}
