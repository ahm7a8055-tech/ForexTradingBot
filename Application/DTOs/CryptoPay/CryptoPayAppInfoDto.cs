using System.Text.Json.Serialization;

namespace Application.DTOs.CryptoPay
{
    #region CryptoPayAppInfoDto
    /// <summary>
    /// Represents the information about a Crypto Pay application (bot).
    /// This DTO is designed to deserialize the JSON response from the Crypto Pay `getMe` method.
    /// </summary>
    public class CryptoPayAppInfoDto
    {
        #region Properties

        #region Application Details
        /// <summary>
        /// Gets or sets the unique numerical identifier for your application.
        /// </summary>
        /// <example>12345</example>
        [JsonPropertyName("app_id")]
        public int AppId { get; set; }

        /// <summary>
        /// Gets or sets the name of your application, as configured in Crypto Pay.
        /// </summary>
        /// <example>My Awesome Shop</example>
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        /// <summary>
        /// Gets or sets the username of the official Crypto Pay bot that handles the payment UI.
        /// </summary>
        /// <remarks>
        /// This is the bot users will interact with to complete their payment (e.g., @CryptoBot or @CryptoTestnetBot).
        /// </remarks>
        /// <example>CryptoBot</example>
        [JsonPropertyName("payment_processing_bot_username")]
        public string? PaymentProcessingBotUsername { get; set; }
        #endregion

        #endregion
    }
    #endregion
}