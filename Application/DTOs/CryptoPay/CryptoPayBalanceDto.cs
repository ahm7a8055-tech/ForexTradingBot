using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Application.DTOs.CryptoPay
{
    #region CryptoPayBalanceDto
    /// <summary>
    /// Represents the balance of a single cryptocurrency asset in a Crypto Pay account.
    /// This DTO is designed to deserialize items from the list returned by the `getBalance` method.
    /// </summary>
    public class CryptoPayBalanceDto
    {
        #region Properties

        #region Balance Details
        /// <summary>
        /// Gets or sets the cryptocurrency asset code.
        /// </summary>
        /// <example>USDT</example>
        [JsonPropertyName("currency_code")]
        [Required]
        public string CurrencyCode { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the total available balance for this asset.
        /// </summary>
        /// <remarks>
        /// This is represented as a string to avoid floating-point inaccuracies and support high precision.
        /// It should be parsed to a `decimal` for any calculations.
        /// </remarks>
        /// <example>1500.75</example>
        [JsonPropertyName("available")]
        [Required]
        public string Available { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the balance that is currently on hold (e.g., from pending transactions).
        /// </summary>
        /// <remarks>
        /// This is represented as a string for high precision. It should be parsed to a `decimal` for calculations.
        /// </remarks>
        /// <example>50.25</example>
        [JsonPropertyName("onhold")]
        [Required]
        public string Onhold { get; set; } = string.Empty;
        #endregion

        #endregion
    }
    #endregion
}