// File: Application/DTOs/Fmp/FmpActivelyTradedDto.cs
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Application.DTOs.Fmp
{
    #region FmpActivelyTradedDto
    /// <summary>
    /// Represents a single actively traded asset from the Financial Modeling Prep (FMP) API.
    /// This DTO is designed to deserialize items from the '/stable/actively-trading-list' endpoint,
    /// which can include stocks, ETFs, and cryptocurrencies.
    /// </summary>
    public class FmpActivelyTradedDto
    {
        #region Properties

        #region Identification
        /// <summary>
        /// Gets or sets the unique ticker symbol for the financial asset.
        /// </summary>
        /// <example>AAPL</example>
        [JsonPropertyName("symbol")]
        [Required(ErrorMessage = "The asset symbol is required.")]
        [StringLength(20, ErrorMessage = "The symbol cannot exceed 20 characters.")]
        public string Symbol { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the full name of the company or asset. This can be null in some API responses.
        /// </summary>
        /// <example>Apple Inc.</example>
        [JsonPropertyName("name")]
        [StringLength(255, ErrorMessage = "The name cannot exceed 255 characters.")]
        public string? Name { get; set; }
        #endregion

        #region Market Data
        /// <summary>
        /// Gets or sets the last traded price of the asset.
        /// </summary>
        /// <example>190.50</example>
        [JsonPropertyName("price")]
        public decimal? Price { get; set; }

        /// <summary>
        /// Gets or sets the nominal (absolute) change in price from the previous trading day's close.
        /// </summary>
        /// <example>1.25</example>
        [JsonPropertyName("change")]
        public decimal? Change { get; set; }

        /// <summary>
        /// Gets or sets the percentage change in price from the previous trading day's close.
        /// </summary>
        /// <example>0.66</example>
        [JsonPropertyName("changesPercentage")]
        public decimal? ChangesPercentage { get; set; }
        #endregion

        #endregion
    }
    #endregion
}