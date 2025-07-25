using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Application.DTOs.CryptoPay
{
    #region CryptoPayInvoiceDto
    /// <summary>
    /// Represents a detailed invoice object returned by the Crypto Pay API.
    /// This DTO is used to deserialize responses from methods like `createInvoice` and `getInvoices`.
    /// </summary>
    public class CryptoPayInvoiceDto
    {
        #region Properties

        #region Core Identification
        /// <summary>
        /// Gets or sets the unique numerical identifier for the invoice.
        /// </summary>
        /// <example>12345</example>
        [JsonPropertyName("invoice_id")]
        public long InvoiceId { get; set; }

        /// <summary>
        /// Gets or sets the unique hash identifier for the invoice.
        /// </summary>
        /// <example>AAgA5wUu</example>
        [JsonPropertyName("hash")]
        public string? Hash { get; set; }
        #endregion

        #region Invoice State & Timestamps
        /// <summary>
        /// Gets or sets the current status of the invoice.
        /// </summary>
        /// <remarks>Possible values are: "active", "paid", "expired".</remarks>
        /// <example>active</example>
        [JsonPropertyName("status")]
        public string? Status { get; set; }

        /// <summary>
        /// Gets or sets the creation date of the invoice in ISO 8601 format.
        /// </summary>
        /// <example>2024-05-22T10:00:00.123Z</example>
        [JsonPropertyName("created_at")]
        public string? CreatedAtIso { get; set; }

        /// <summary>
        /// Gets or sets the date the invoice was paid in ISO 8601 format. Null if not paid.
        /// </summary>
        /// <example>2024-05-22T10:05:30.456Z</example>
        [JsonPropertyName("paid_at")]
        public string? PaidAtIso { get; set; }

        /// <summary>
        /// Gets or sets the expiration date of the invoice in ISO 8601 format.
        /// </summary>
        /// <example>2024-05-22T11:00:00.123Z</example>
        [JsonPropertyName("expiration_date")]
        public string? ExpirationDateIso { get; set; }
        #endregion

        #region Payment Details & Links
        /// <summary>
        /// Gets or sets the cryptocurrency asset code for the invoice.
        /// </summary>
        /// <example>USDT</example>
        [JsonPropertyName("asset")]
        public string? Asset { get; set; }

        /// <summary>
        /// Gets or sets the invoice amount as a string for high precision.
        /// </summary>
        /// <example>125.50</example>
        [JsonPropertyName("amount")]
        public string? Amount { get; set; }

        /// <summary>
        /// Gets or sets the URL for the payment page.
        /// </summary>
        /// <remarks>This property is deprecated by Crypto Pay but may appear in responses.</remarks>
        /// <example>https://t.me/CryptoBot?start=IV12345</example>
        [JsonPropertyName("pay_url")]
        [Url]
        public string? PayUrl { get; set; }

        /// <summary>
        /// Gets or sets the URL to open the invoice in the official Crypto Pay bot.
        /// </summary>
        /// <example>https://t.me/CryptoBot?start=IV12345</example>
        [JsonPropertyName("bot_invoice_url")]
        [Url]
        public string? BotInvoiceUrl { get; set; }

        /// <summary>
        /// Gets or sets the URL to open the invoice in the bot's Mini App.
        /// </summary>
        [JsonPropertyName("mini_app_invoice_url")]
        [Url]
        public string? MiniAppInvoiceUrl { get; set; }

        /// <summary>
        /// Gets or sets the URL to open the invoice in the bot's Web App.
        /// </summary>
        [JsonPropertyName("web_app_invoice_url")]
        [Url]
        public string? WebAppInvoiceUrl { get; set; }
        #endregion

        #region Descriptive Information
        /// <summary>
        /// Gets or sets the public description of the invoice.
        /// </summary>
        /// <example>Payment for Order #12345</example>
        [JsonPropertyName("description")]
        public string? Description { get; set; }

        /// <summary>
        /// Gets or sets a message shown to the user only after successful payment.
        /// </summary>
        /// <example>Your access key is: XYZ-ABC-123</example>
        [JsonPropertyName("hidden_message")]
        public string? HiddenMessage { get; set; }

        /// <summary>
        /// Gets or sets a comment left by the user during payment.
        /// </summary>
        /// <example>Please ship quickly.</example>
        [JsonPropertyName("comment")]
        public string? Comment { get; set; }
        #endregion

        #region Behavioral Configuration & Status
        /// <summary>
        /// Gets or sets a value indicating if comments were allowed for this invoice.
        /// </summary>
        [JsonPropertyName("allow_comments")]
        public bool AllowComments { get; set; }

        /// <summary>
        /// Gets or sets a value indicating if anonymous payments were allowed for this invoice.
        /// </summary>
        [JsonPropertyName("allow_anonymous")]
        public bool AllowAnonymous { get; set; }

        /// <summary>
        /// Gets or sets a value indicating if the invoice was paid anonymously. Null if not yet paid.
        /// </summary>
        [JsonPropertyName("paid_anonymously")]
        public bool? PaidAnonymously { get; set; }
        #endregion

        #region Post-Payment Flow & Integration
        /// <summary>
        /// Gets or sets custom data that was passed when creating the invoice.
        /// </summary>
        /// <example>{"order_id": "12345"}</example>
        [JsonPropertyName("payload")]
        public string? Payload { get; set; }

        /// <summary>
        /// Gets or sets the type of button shown to the user after payment.
        /// </summary>
        /// <example>viewItem</example>
        [JsonPropertyName("paid_btn_name")]
        public string? PaidBtnName { get; set; }

        /// <summary>
        /// Gets or sets the URL associated with the post-payment button.
        /// </summary>
        /// <example>https://my-shop.com/order/12345</example>
        [JsonPropertyName("paid_btn_url")]
        [Url]
        public string? PaidBtnUrl { get; set; }
        #endregion

        #region Fiat & Fee Details
        /// <summary>
        /// Gets or sets the currency type used for the invoice amount ("crypto" or "fiat").
        /// </summary>
        /// <example>fiat</example>
        [JsonPropertyName("currency_type")]
        public string? CurrencyType { get; set; }

        /// <summary>
        /// Gets or sets the fiat currency code if `CurrencyType` is "fiat".
        /// </summary>
        /// <example>USD</example>
        [JsonPropertyName("fiat")]
        public string? Fiat { get; set; }

        /// <summary>
        /// Gets or sets a comma-separated list of accepted cryptocurrencies if `CurrencyType` is "fiat".
        /// </summary>
        /// <example>USDT,TON,BTC</example>
        [JsonPropertyName("accepted_assets")]
        public string? AcceptedAssets { get; set; }

        /// <summary>
        /// Gets or sets the asset that the user actually paid with.
        /// </summary>
        /// <example>TON</example>
        [JsonPropertyName("paid_asset")]
        public string? PaidAsset { get; set; }

        /// <summary>
        /// Gets or sets the amount that the user actually paid, in the `PaidAsset` currency.
        /// </summary>
        /// <example>5.25</example>
        [JsonPropertyName("paid_amount")]
        public string? PaidAmount { get; set; }

        /// <summary>
        /// Gets or sets the exchange rate used for the transaction, if applicable (e.g., "USDT" to "USD").
        /// </summary>
        /// <example>1.001</example>
        [JsonPropertyName("paid_fiat_rate")]
        public string? PaidFiatRate { get; set; }

        /// <summary>
        /// Gets or sets the exchange rate to USD at the time of payment.
        /// </summary>
        /// <example>70000.12</example>
        [JsonPropertyName("paid_usd_rate")]
        public string? PaidUsdRate { get; set; }

        /// <summary>
        /// Gets or sets the asset used to pay the transaction fee.
        /// </summary>
        /// <example>USDT</example>
        [JsonPropertyName("fee_asset")]
        public string? FeeAsset { get; set; }

        /// <summary>
        /// Gets or sets the amount of the transaction fee.
        /// </summary>
        /// <remarks>The API documentation specifies this as a Number, but it is represented here as a string for consistency with other amount fields.</remarks>
        /// <example>0.10</example>
        [JsonPropertyName("fee_amount")]
        public string? FeeAmount { get; set; }
        #endregion

        #endregion
    }
    #endregion
}