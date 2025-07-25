// File: Application/DTOs/CoinGecko/CoinDetailsDto.cs
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Application.DTOs.CoinGecko
{
    #region CoinDetailsDto
    /// <summary>
    /// Represents the detailed information for a single cryptocurrency, designed to deserialize
    /// the JSON response from CoinGecko's `/coins/{id}` endpoint.
    /// </summary>
    public class CoinDetailsDto
    {
        #region Properties

        #region Core Identification
        /// <summary>
        /// Gets or sets the unique identifier for the coin (e.g., "bitcoin").
        /// </summary>
        /// <example>bitcoin</example>
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the ticker symbol for the coin (e.g., "btc").
        /// </summary>
        /// <example>btc</example>
        [JsonPropertyName("symbol")]
        public string Symbol { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the full name of the coin.
        /// </summary>
        /// <example>Bitcoin</example>
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
        #endregion

        #region Descriptive Content
        /// <summary>
        /// Gets or sets a dictionary of localized descriptions for the coin.
        /// </summary>
        /// <remarks>
        /// The dictionary key is the language code (e.g., "en", "de", "es"),
        /// and the value is the description text in that language. The content may contain HTML.
        /// </remarks>
        [JsonPropertyName("description")]
        public Dictionary<string, string>? Description { get; set; }
        #endregion

        #region Market Data
        /// <summary>
        /// Gets or sets the nested object containing all market data for the coin.
        /// </summary>
        [JsonPropertyName("market_data")]
        public MarketDataDto? MarketData { get; set; }
        #endregion

        #endregion
    }
    #endregion

    #region MarketDataDto
    /// <summary>
    /// Represents the 'market_data' nested object from the CoinGecko `/coins/{id}` API response.
    /// It contains price, volume, and other metrics in various fiat and crypto currencies.
    /// </summary>
    public class MarketDataDto
    {
        #region Properties

        #region Market Metrics (by Currency)
        /// <summary>
        /// Gets or sets a dictionary of the coin's current price in different currencies.
        /// </summary>
        /// <remarks>
        /// The key is the currency code (e.g., "usd", "eur", "btc"), and the value is the price.
        /// The data type is `double?` to correctly handle potential scientific notation from the API.
        /// </remarks>
        /// <example>{"usd": 65000.50, "eur": 60000.75}</example>
        [JsonPropertyName("current_price")]
        public Dictionary<string, double?>? CurrentPrice { get; set; }

        /// <summary>
        /// Gets or sets a dictionary of the coin's market capitalization in different currencies.
        /// </summary>
        /// <remarks>
        /// The key is the currency code, and the value is the market cap.
        /// The data type is `double?` to correctly handle scientific notation.
        /// </remarks>
        [JsonPropertyName("market_cap")]
        public Dictionary<string, double?>? MarketCap { get; set; }

        /// <summary>
        /// Gets or sets a dictionary of the coin's total trading volume in different currencies.
        /// </summary>
        /// <remarks>
        /// The key is the currency code, and the value is the total volume.
        /// The data type is `double?` to correctly handle scientific notation.
        /// </remarks>
        [JsonPropertyName("total_volume")]
        public Dictionary<string, double?>? TotalVolume { get; set; }

        /// <summary>
        /// Gets or sets a dictionary of the coin's 24-hour high price in different currencies.
        /// </summary>
        /// <remarks>
        /// The key is the currency code, and the value is the 24h high.
        /// The data type is `double?` to correctly handle scientific notation.
        /// </remarks>
        [JsonPropertyName("high_24h")]
        public Dictionary<string, double?>? High24h { get; set; }

        /// <summary>
        /// Gets or sets a dictionary of the coin's 24-hour low price in different currencies.
        /// </summary>
        /// <remarks>
        /// The key is the currency code, and the value is the 24h low.
        /// The data type is `double?` to correctly handle scientific notation.
        /// </remarks>
        [JsonPropertyName("low_24h")]
        public Dictionary<string, double?>? Low24h { get; set; }
        #endregion

        #region General Metrics
        /// <summary>
        /// Gets or sets the overall price change percentage in the last 24 hours against the default currency (usually USD).
        /// </summary>
        /// <example>-2.5</example>
        [JsonPropertyName("price_change_percentage_24h")]
        public double? PriceChangePercentage24h { get; set; }
        #endregion

        #endregion
    }
    #endregion
}