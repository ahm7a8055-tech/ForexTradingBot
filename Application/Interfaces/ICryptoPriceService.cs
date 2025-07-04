namespace TelegramPanel.Application.Interfaces
{
    /// <summary>
    /// Defines a service for fetching cryptocurrency prices.
    /// </summary>
    public interface ICryptoPriceService
    {
        /// <summary>
        /// Gets the current prices for a list of cryptocurrencies against a specified currency.
        /// </summary>
        /// <param name="cryptoCoinGeckoIds">A list of cryptocurrency IDs from CoinGecko (e.g., "bitcoin", "tether").</param>
        /// <param name="vsCurrency">The target currency to get the price in (e.g., "usd").</param>
        /// <returns>A dictionary where the key is the crypto ID and the value is its price, or null if the API call fails.</returns>
        Task<Dictionary<string, decimal>?> GetPricesAsync(IEnumerable<string> cryptoCoinGeckoIds, string vsCurrency = "usd");
    }
}