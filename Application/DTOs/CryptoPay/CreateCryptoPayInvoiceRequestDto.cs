using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Application.DTOs.CryptoPay
{
    #region CreateCryptoPayInvoiceRequestDto
    /// <summary>
    /// Represents the request body for creating a new invoice using the Crypto Pay API.
    /// This DTO encapsulates all possible parameters for invoice creation.
    /// </summary>
    /// <remarks>
    /// This DTO maps directly to the fields expected by the Crypto Pay `createInvoice` method.
    /// Field names are specified in snake_case using <see cref="JsonPropertyNameAttribute"/>.
    /// </remarks>
    public class CreateCryptoPayInvoiceRequestDto
    {
        #region Properties

        #region Core Invoice Details
        /// <summary>
        /// Gets or sets the cryptocurrency asset code for the invoice.
        /// </summary>
        /// <example>USDT</example>
        [Required(ErrorMessage = "The asset code is required.")]
        [StringLength(10, ErrorMessage = "Asset code cannot exceed 10 characters.")]
        [JsonPropertyName("asset")]
        public string Asset { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the invoice amount.
        /// </summary>
        /// <remarks>
        /// This is represented as a string to avoid floating-point inaccuracies and support high precision.
        /// </remarks>
        /// <example>125.50</example>
        [Required(ErrorMessage = "The amount is required.")]
        [RegularExpression(@"^\d+(\.\d{1,18})?$", ErrorMessage = "Amount must be a valid positive number string.")]
        [JsonPropertyName("amount")]
        public string Amount { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets an optional public description for the invoice, which will be visible to the user.
        /// </summary>
        /// <example>Payment for Order #12345</example>
        [StringLength(1024, ErrorMessage = "Description cannot exceed 1024 characters.")]
        [JsonPropertyName("description")]
        public string? Description { get; set; }
        #endregion

        #region Post-Payment Flow
        /// <summary>
        /// Gets or sets an optional message shown to the user only after successful payment.
        /// </summary>
        /// <example>Your access key is: XYZ-ABC-123</example>
        [StringLength(2048, ErrorMessage = "Hidden message cannot exceed 2048 characters.")]
        [JsonPropertyName("hidden_message")]
        public string? HiddenMessage { get; set; }

        /// <summary>
        /// Gets or sets the type of button to be shown to the user after successful payment.
        /// </summary>
        /// <remarks>Valid values are: "viewItem", "openChannel", "openBot", "callback".</remarks>
        /// <example>viewItem</example>
        [JsonPropertyName("paid_btn_name")]
        public string? PaidBtnName { get; set; }

        /// <summary>
        /// Gets or sets the URL for the post-payment button.
        /// </summary>
        /// <remarks>This is required if `PaidBtnName` is "viewItem", "openChannel", or "openBot".</remarks>
        /// <example>https://my-shop.com/order/12345</example>
        [Url(ErrorMessage = "The paid button URL must be a valid URL.")]
        [JsonPropertyName("paid_btn_url")]
        public string? PaidBtnUrl { get; set; }
        #endregion

        #region System & Integration
        /// <summary>
        /// Gets or sets a custom data string (e.g., a JSON object) to be passed back with webhook updates.
        /// </summary>
        /// <remarks>This is useful for tracking your internal entities, like an Order ID or User ID.</remarks>
        /// <example>{"order_id": "12345", "user_id": "abc-def"}</example>
        [StringLength(4096, ErrorMessage = "Payload cannot exceed 4096 characters.")]
        [JsonPropertyName("payload")]
        public string? Payload { get; set; }
        #endregion

        #region Behavioral Configuration
        /// <summary>
        /// Gets or sets a value indicating whether to allow comments for the invoice. Defaults to true.
        /// </summary>
        [JsonPropertyName("allow_comments")]
        public bool? AllowComments { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether to allow anonymous payments. Defaults to true.
        /// </summary>
        [JsonPropertyName("allow_anonymous")]
        public bool? AllowAnonymous { get; set; } = true;

        /// <summary>
        /// Gets or sets the invoice expiration period in seconds.
        /// </summary>
        /// <remarks>The value must be between 1 and 2,678,400 (31 days).</remarks>
        /// <example>3600</example>
        [Range(1, 2678400, ErrorMessage = "Expiration must be between 1 and 2,678,400 seconds.")]
        [JsonPropertyName("expires_in")]
        public int? ExpiresInSeconds { get; set; }
        #endregion

        #region Fiat Currency Options
        /// <summary>
        /// Gets or sets the type of currency for the invoice ("crypto" or "fiat").
        /// </summary>
        /// <remarks>Use "fiat" to specify the amount in a fiat currency and have CryptoPay calculate the crypto amount.</remarks>
        /// <example>fiat</example>
        [JsonPropertyName("currency_type")]
        public string? CurrencyType { get; set; }

        /// <summary>
        /// Gets or sets the fiat currency code (e.g., ISO 4217) if `CurrencyType` is "fiat".
        /// </summary>
        /// <example>USD</example>
        [JsonPropertyName("fiat")]
        public string? FiatCurrency { get; set; }

        /// <summary>
        /// Gets or sets a comma-separated list of accepted cryptocurrencies if `CurrencyType` is "fiat".
        /// </summary>
        /// <example>USDT,TON,BTC</example>
        [JsonPropertyName("accepted_assets")]
        public string? AcceptedAssets { get; set; }
        #endregion

        #endregion
    }
    #endregion
}