using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Application.DTOs.CryptoPay
{
    #region GetCryptoPayInvoicesRequestDto
    /// <summary>
    /// Represents the query parameters for retrieving a list of invoices using the Crypto Pay API.
    /// This DTO is used to construct the request to the `getInvoices` method.
    /// </summary>
    /// <remarks>
    /// All properties are optional and are used to filter and paginate the results.
    /// This object is typically serialized into a query string, not a JSON body.
    /// </remarks>
    public class GetCryptoPayInvoicesRequestDto
    {
        #region Properties

        #region Filtering Options
        /// <summary>
        /// Gets or sets the asset code to filter invoices by.
        /// </summary>
        /// <example>USDT</example>
        [JsonPropertyName("asset")]
        [StringLength(10, ErrorMessage = "Asset code cannot exceed 10 characters.")]
        public string? Asset { get; set; }

        /// <summary>
        /// Gets or sets a comma-separated string of invoice IDs to retrieve.
        /// </summary>
        /// <remarks>
        /// When using this parameter, up to 100 invoice IDs can be specified.
        /// </remarks>
        /// <example>12345,12346,12347</example>
        [JsonPropertyName("invoice_ids")]
        public string? InvoiceIds { get; set; }

        /// <summary>
        /// Gets or sets the status to filter invoices by.
        /// </summary>
        /// <remarks>Valid values are "active" or "paid".</remarks>
        /// <example>paid</example>
        [JsonPropertyName("status")]
        public string? Status { get; set; }
        #endregion

        #region Pagination Options
        /// <summary>
        /// Gets or sets the number of invoices to skip from the beginning of the list.
        /// </summary>
        /// <example>0</example>
        [JsonPropertyName("offset")]
        [Range(0, int.MaxValue, ErrorMessage = "Offset cannot be negative.")]
        public int? Offset { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of invoices to retrieve.
        /// </summary>
        /// <remarks>
        /// The value must be between 1 and 1000. The default is 100.
        /// </remarks>
        /// <example>50</example>
        [JsonPropertyName("count")]
        [Range(1, 1000, ErrorMessage = "Count must be between 1 and 1000.")]
        public int? Count { get; set; }
        #endregion

        #endregion
    }
    #endregion
}