// File: Application/DTOs/CryptoPay/CryptoPayWebhookUpdateDto.cs
#region Usings
using System.Text.Json.Serialization;
#endregion

namespace Application.DTOs.CryptoPay
{
    #region CryptoPayWebhookUpdateDto
    /// <summary>
    /// Represents the root object of a webhook notification sent by the Crypto Pay API.
    /// This DTO is designed to deserialize the entire JSON payload received when an invoice status changes.
    /// </summary>
    public class CryptoPayWebhookUpdateDto
    {
        #region Properties

        #region Webhook Metadata
        /// <summary>
        /// Gets or sets the unique numerical identifier for this specific webhook update event.
        /// </summary>
        /// <example>123456789</example>
        [JsonPropertyName("update_id")]
        public long UpdateId { get; set; }

        /// <summary>
        /// Gets or sets the type of update this webhook represents.
        /// </summary>
        /// <remarks>The primary value to check for is "invoice_paid".</remarks>
        /// <example>invoice_paid</example>
        [JsonPropertyName("update_type")]
        public string? UpdateType { get; set; }

        /// <summary>
        /// Gets or sets the date and time when Crypto Pay sent the webhook request, in ISO 8601 format.
        /// </summary>
        /// <example>2024-05-22T10:05:31.500Z</example>
        [JsonPropertyName("request_date")]
        public string? RequestDateIso { get; set; }
        #endregion

        #region Webhook Payload
        /// <summary>
        /// Gets or sets the main payload of the webhook, which contains the full, updated details of the invoice.
        /// </summary>
        /// <remarks>
        /// This property is deserialized into a <see cref="CryptoPayInvoiceDto"/> object.
        /// </remarks>
        [JsonPropertyName("payload")]
        public CryptoPayInvoiceDto? Payload { get; set; }
        #endregion

        #endregion
    }
    #endregion
}