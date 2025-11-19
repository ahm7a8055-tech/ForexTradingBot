using Application.Interfaces;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json; // NuGet: System.Net.Http.Json

namespace Infrastructure.Services.CoinGecko
{
    public class CoinGeckoPriceService : ICryptoPriceService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<CoinGeckoPriceService> _logger;

        // Base URL for CoinGecko's simple price endpoint
        private const string CoinGeckoApiUrl = "https://api.coingecko.com/api/v3/simple/price";

        public CoinGeckoPriceService(HttpClient httpClient, ILogger<CoinGeckoPriceService> logger)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<Dictionary<string, decimal>?> GetPricesAsync(IEnumerable<string> cryptoCoinGeckoIds, string vsCurrency = "usd")
        {
            if (cryptoCoinGeckoIds == null || !cryptoCoinGeckoIds.Any())
            {
                return [];
            }

            string idsParam = string.Join(",", cryptoCoinGeckoIds);
            string requestUri = $"{CoinGeckoApiUrl}?ids={idsParam}&vs_currencies={vsCurrency}";

            try
            {
                _logger.LogInformation("Fetching crypto prices from CoinGecko for IDs: {CryptoIds}", idsParam);

                // The API response is nested, e.g., {"bitcoin": {"usd": 65000}, "tether": {"usd": 1.00}}
                Dictionary<string, Dictionary<string, decimal>>? response = await _httpClient.GetFromJsonAsync<Dictionary<string, Dictionary<string, decimal>>>(requestUri);

                if (response == null)
                {
                    _logger.LogWarning("CoinGecko API returned a null or empty response for IDs: {CryptoIds}", idsParam);
                    return null;
                }

                // We flatten the dictionary for easier consumption by our application
                Dictionary<string, decimal> prices = response
                    .Where(p => p.Value != null && p.Value.ContainsKey(vsCurrency))
                    .ToDictionary(p => p.Key, p => p.Value[vsCurrency]);

                _logger.LogInformation("Successfully fetched {PriceCount} prices.", prices.Count);
                return prices;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch or parse prices from CoinGecko for IDs: {CryptoIds}", idsParam);
                return null; // Return null to indicate failure, allowing the caller to handle it gracefully.
            }
        }
    }
}