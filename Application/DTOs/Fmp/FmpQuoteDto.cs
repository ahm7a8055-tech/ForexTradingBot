using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Application.DTOs.Fmp
{
    #region FmpQuoteDto
    /// <summary>
    /// Represents a detailed quote for a single financial asset from the Financial Modeling Prep (FMP) API.
    /// This DTO is designed to deserialize the JSON response from the '/quote/{symbol}' endpoint.
    /// </summary>
    public class FmpQuoteDto
    {
        #region Properties

        #region Identification
        /// <summary>
        /// Gets or sets the unique ticker symbol for the financial asset.
        /// </summary>
        /// <example>AAPL</example>
        [JsonPropertyName("symbol")]
        public string Symbol { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the full name of the company or asset.
        /// </summary>
        /// <example>Apple Inc.</example>
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        /// <summary>
        /// Gets or sets the name of the exchange where the asset is traded.
        /// </summary>
        /// <example>NASDAQ</example>
        [JsonPropertyName("exchange")]
        public string? Exchange { get; set; }
        #endregion

        #region Current Market Data
        /// <summary>
        /// Gets or sets the last traded price of the asset.
        /// </summary>
        /// <example>190.50</example>
        [JsonPropertyName("price")]
        public decimal? Price { get; set; }

        /// <summary>
        /// Gets or sets the opening price for the current trading day.
        /// </summary>
        /// <example>189.90</example>
        [JsonPropertyName("open")]
        public decimal? Open { get; set; }

        /// <summary>
        /// Gets or sets the lowest price reached during the current trading day.
        /// </summary>
        /// <example>188.75</example>
        [JsonPropertyName("dayLow")]
        public decimal? DayLow { get; set; }

        /// <summary>
        /// Gets or sets the highest price reached during the current trading day.
        /// </summary>
        /// <example>191.25</example>
        [JsonPropertyName("dayHigh")]
        public decimal? DayHigh { get; set; }

        /// <summary>
        /// Gets or sets the closing price from the previous trading day.
        /// </summary>
        /// <example>189.25</example>
        [JsonPropertyName("previousClose")]
        public decimal? PreviousClose { get; set; }

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

        #region Historical & Average Price Data
        /// <summary>
        /// Gets or sets the lowest price reached over the last 52 weeks.
        /// </summary>
        /// <example>164.08</example>
        [JsonPropertyName("yearLow")]
        public decimal? YearLow { get; set; }

        /// <summary>
        /// Gets or sets the highest price reached over the last 52 weeks.
        /// </summary>
        /// <example>199.62</example>
        [JsonPropertyName("yearHigh")]
        public decimal? YearHigh { get; set; }

        /// <summary>
        /// Gets or sets the 50-day simple moving average of the price.
        /// </summary>
        /// <example>185.30</example>
        [JsonPropertyName("priceAvg50")]
        public decimal? PriceAvg50 { get; set; }

        /// <summary>
        /// Gets or sets the 200-day simple moving average of the price.
        /// </summary>
        /// <example>180.10</example>
        [JsonPropertyName("priceAvg200")]
        public decimal? PriceAvg200 { get; set; }
        #endregion

        #region Trading Volume
        /// <summary>
        /// Gets or sets the number of shares traded during the current trading day.
        /// </summary>
        /// <example>45000000</example>
        [JsonPropertyName("volume")]
        public long? Volume { get; set; }

        /// <summary>
        /// Gets or sets the average daily trading volume over a recent period (e.g., 30 days).
        /// </summary>
        /// <example>55000000</example>
        [JsonPropertyName("avgVolume")]
        public long? AvgVolume { get; set; }
        #endregion

        #region Fundamental Data
        /// <summary>
        /// Gets or sets the total market value of the company's outstanding shares.
        /// </summary>
        /// <example>2900000000000</example>
        [JsonPropertyName("marketCap")]
        public long? MarketCap { get; set; }

        /// <summary>
        /// Gets or sets the number of shares available for trading.
        /// </summary>
        /// <example>15500000000</example>
        [JsonPropertyName("sharesOutstanding")]
        public long? SharesOutstanding { get; set; }

        /// <summary>
        /// Gets or sets the company's Earnings Per Share (EPS).
        /// </summary>
        /// <example>6.43</example>
        [JsonPropertyName("eps")]
        public decimal? Eps { get; set; }

        /// <summary>
        /// Gets or sets the company's Price-to-Earnings (P/E) ratio.
        /// </summary>
        /// <example>29.63</example>
        [JsonPropertyName("pe")]
        public decimal? Pe { get; set; }

        /// <summary>
        /// Gets or sets a string describing the next earnings announcement date or time.
        /// </summary>
        /// <example>2024-07-25T16:30:00.000-0400</example>
        [JsonPropertyName("earningsAnnouncement")]
        public string? EarningsAnnouncement { get; set; }
        #endregion

        #region Metadata
        /// <summary>
        /// Gets or sets the Unix timestamp (in seconds) indicating when the quote data was generated.
        /// </summary>
        /// <example>1684785600</example>
        [JsonPropertyName("timestamp")]
        public long? Timestamp { get; set; }
        #endregion

        #endregion
    }
    #endregion
}