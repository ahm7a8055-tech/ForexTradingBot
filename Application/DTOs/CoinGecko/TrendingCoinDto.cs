// File: Application/DTOs/CoinGecko/TrendingCoinResult.cs
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Application.DTOs.CoinGecko
{
    #region TrendingCoinResult
    /// <summary>
    /// Represents a top-level wrapper object for a trending coin from the CoinGecko API.
    /// This is designed to deserialize the nested structure from the `/search/trending` endpoint.
    /// </summary>
    public class TrendingCoinResult
    {
        #region Properties

        #region Nested Payload
        /// <summary>
        /// Gets or sets the nested object containing the core data for a single trending coin.
        /// </summary>
        [JsonPropertyName("item")]
        public TrendingCoinDto? Item { get; set; }
        #endregion

        #endregion
    }
    #endregion

    #region TrendingCoinDto
    /// <summary>
    /// Represents the core data of a single trending cryptocurrency as returned by the CoinGecko API.
    /// This DTO is lightweight and contains essential identifiers and ranking information.
    /// </summary>
    public class TrendingCoinDto
    {
        #region Properties

        #region Core Identification
        /// <summary>
        /// Gets or sets the unique identifier for the coin (e.g., "bitcoin").
        /// </summary>
        /// <example>bitcoin</example>
        [JsonPropertyName("id")]
        [Required]
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the full name of the coin.
        /// </summary>
        /// <example>Bitcoin</example>
        [JsonPropertyName("name")]
        [Required]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the ticker symbol for the coin (e.g., "btc").
        /// </summary>
        /// <example>btc</example>
        [JsonPropertyName("symbol")]
        [Required]
        public string Symbol { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the global market capitalization rank of the coin.
        /// </summary>
        /// <example>1</example>
        [JsonPropertyName("market_cap_rank")]
        public int? MarketCapRank { get; set; }
        #endregion

        #endregion
    }
    #endregion
}