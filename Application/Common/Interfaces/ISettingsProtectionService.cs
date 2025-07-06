namespace Application.Common.Interfaces
{
    public interface ISettingsProtectionService
    {
        /// <summary>
        /// Encrypts a plaintext string.
        /// </summary>
        /// <param name="plaintext">The string to encrypt.</param>
        /// <returns>The encrypted ciphertext.</returns>
        string Encrypt(string plaintext);

        /// <summary>
        /// Decrypts a ciphertext string.
        /// </summary>
        /// <param name="ciphertext">The string to decrypt.</param>
        /// <returns>The decrypted plaintext.</returns>
        /// <exception cref="System.Security.Cryptography.CryptographicException">Thrown if decryption fails.</exception>
        string Decrypt(string ciphertext);
    }
}
