// File: Application/DTOs/Crypto/UnifiedCryptoDto.cs
using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.Crypto.Dtos
{
    #region UnifiedCryptoDto
    /// <summary>
    /// A unified Data Transfer Object that represents the merged and most complete data available
    /// for a cryptocurrency from multiple sources (e.g., CoinGecko and FMP).
    /// </summary>
    /// <remarks>
    /// This DTO provides a consistent data model to the rest of the application,
    /// abstracting away the complexities of fetching and combining data from different APIs.
    /// </remarks>
    public class UnifiedCryptoDto
    {
        #region Properties

        #region Core Identification & Descriptive Data
        /// <summary>
        /// Gets or sets the unique identifier from the primary data source (e.g., "bitcoin" from CoinGecko).
        /// </summary>
        /// <example>bitcoin</example>
        [Required]
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the ticker symbol for the cryptocurrency (e.g., "btc").
        /// </summary>
        /// <example>btc</example>
        [Required]
        [StringLength(20, ErrorMessage = "Symbol cannot exceed 20 characters.")]
        public string Symbol { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the full name of the cryptocurrency.
        /// </summary>
        /// <example>Bitcoin</example>
        [StringLength(100, ErrorMessage = "Name cannot exceed 100 characters.")]
        public string? Name { get; set; }

        /// <summary>
        /// Gets or sets a rich description of the cryptocurrency, typically sourced from CoinGecko.
        /// </summary>
        /// <remarks>This content may include HTML tags that need to be handled by the presentation layer.</remarks>
        /// <example>Bitcoin is a decentralized digital currency...</example>
        [StringLength(4000, ErrorMessage = "Description cannot exceed 4000 characters.")]
        public string? Description { get; set; }

        /// <summary>
        /// Gets or sets the URL for the cryptocurrency's logo image.
        /// </summary>
        /// <example>https://assets.coingecko.com/coins/images/1/large/bitcoin.png</example>
        [Url(ErrorMessage = "Image URL must be a valid URL.")]
        [StringLength(500, ErrorMessage = "Image URL cannot exceed 500 characters.")]
        public string? ImageUrl { get; set; }

        /// <summary>
        /// Gets or sets the global market capitalization rank.
        /// </summary>
        /// <example>1</example>
        public int? MarketCapRank { get; set; }
        #endregion

        #region Market & Performance Metrics
        /// <summary>
        /// Gets or sets the current price of the cryptocurrency.
        /// </summary>
        /// <remarks>This value may be sourced from a real-time provider like FMP to override less frequent updates from CoinGecko.</remarks>
        /// <example>65000.50</example>
        public decimal? Price { get; set; }

        /// <summary>
        /// Gets or sets the price change percentage over the last 24 hours.
        /// </summary>
        /// <example>-2.5</example>
        public decimal? Change24hPercentage { get; set; }

        /// <summary>
        /// Gets or sets the highest price in the last 24 hours.
        /// </summary>
        /// <example>66500.00</example>
        public decimal? DayHigh { get; set; }

        /// <summary>
        /// Gets or sets the lowest price in the last 24 hours.
        /// </summary>
        /// <example>64800.75</example>
        public decimal? DayLow { get; set; }

        /// <summary>
        /// Gets or sets the total market capitalization (Price * Circulating Supply).
        /// </summary>
        /// <example>1280000000000</example>
        public long? MarketCap { get; set; }

        /// <summary>
        /// Gets or sets the total trading volume in the last 24 hours.
        /// </summary>
        /// <example>25000000000</example>
        public long? TotalVolume { get; set; }
        #endregion

        #region Data Source & State
        /// <summary>
        /// Gets or sets the name of the source that provided the most recent price data.
        /// </summary>
        /// <remarks>Expected values could be "CoinGecko", "FMP", or "Unavailable".</remarks>
        /// <example>FMP</example>
        public string PriceDataSource { get; set; } = "Unavailable";

        /// <summary>
        /// Gets or sets a value indicating whether the data could not be refreshed recently and is potentially outdated.
        /// </summary>
        /// <remarks>
        /// The UI can use this flag to indicate to the user that the displayed data is cached or stale (e.g., by showing a warning icon).
        /// </remarks>
        /// <example>false</example>
        public bool IsDataStale { get; set; } = true;
        #endregion

        #endregion
    }
    #endregion
}