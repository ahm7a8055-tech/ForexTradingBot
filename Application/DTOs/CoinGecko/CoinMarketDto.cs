// File: Application/DTOs/CoinGecko/CoinMarketDto.cs
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Application.DTOs.CoinGecko
{
    #region CoinMarketDto
    /// <summary>
    /// Represents the summary data for a single cryptocurrency, designed to deserialize items
    /// from the CoinGecko `/coins/markets` endpoint response.
    /// This DTO is optimized for list views, containing essential market data for display.
    /// </summary>
    public class CoinMarketDto
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
        /// Gets or sets the ticker symbol for the coin (e.g., "btc").
        /// </summary>
        /// <example>btc</example>
        [JsonPropertyName("symbol")]
        [Required]
        public string Symbol { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the full name of the coin.
        /// </summary>
        /// <example>Bitcoin</example>
        [JsonPropertyName("name")]
        [Required]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the URL for the coin's logo image.
        /// </summary>
        /// <example>https://assets.coingecko.com/coins/images/1/large/bitcoin.png</example>
        [JsonPropertyName("image")]
        [Url(ErrorMessage = "The image link must be a valid URL.")]
        public string? Image { get; set; }
        #endregion

        #region Market Metrics
        /// <summary>
        /// Gets or sets the current price of the coin in the target currency (e.g., USD).
        /// </summary>
        /// <example>65000.50</example>
        [JsonPropertyName("current_price")]
        public double? CurrentPrice { get; set; }

        /// <summary>
        /// Gets or sets the total market capitalization in the target currency.
        /// </summary>
        /// <example>1280000000000</example>
        [JsonPropertyName("market_cap")]
        public long? MarketCap { get; set; }

        /// <summary>
        /// Gets or sets the global market capitalization rank.
        /// </summary>
        /// <example>1</example>
        [JsonPropertyName("market_cap_rank")]
        public int? MarketCapRank { get; set; }

        /// <summary>
        /// Gets or sets the price change percentage over the last 24 hours.
        /// </summary>
        /// <example>-2.5</example>
        [JsonPropertyName("price_change_percentage_24h")]
        public double? PriceChangePercentage24h { get; set; }
        #endregion

        #endregion
    }
    #endregion
}