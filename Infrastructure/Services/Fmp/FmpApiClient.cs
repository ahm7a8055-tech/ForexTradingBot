using Application.Common.Interfaces;
using Application.DTOs.Fmp;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using Shared.Results;
using System.Net.Http.Json;
namespace Infrastructure.Services.Fmp
{
    public class FmpApiClient : IFmpApiClient
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<FmpApiClient> _logger;
        private const string ApiKey = "bXpRTlBPTToPl3TgztFZneqSanKwMnMF";
        private const string BaseUrl = "https://financialmodelingprep.com/stable";
        public FmpApiClient(HttpClient httpClient, ILogger<FmpApiClient> logger) { _httpClient = httpClient; _logger = logger; _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("ForexTradingBot/1.0"); }
        public async Task<Result<FmpQuoteDto>> GetFullCryptoQuoteAsync(string fmpSymbol, CancellationToken cancellationToken)
        {
            string requestUrl = $"{BaseUrl}/quote/{fmpSymbol}?apikey={ApiKey}";
            _logger.LogInformation("Requesting full quote from FMP API for {Symbol}.", fmpSymbol);

            #region Polly Retry Policy
            // Retry on transient HTTP errors (timeouts, 5xx, DNS, etc.), not on 4xx
            AsyncRetryPolicy retryPolicy = Policy
                .Handle<HttpRequestException>(ex =>
                    ex.StatusCode == null || ((int)ex.StatusCode >= 500 && (int)ex.StatusCode < 600))
                .Or<TaskCanceledException>()
                .WaitAndRetryAsync(
                    retryCount: 3,
                    sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                    onRetry: (exception, timespan, retryCount, context) =>
                    {
                        _logger.LogWarning(exception, "Retry {RetryCount}/3: Transient error fetching FMP quote for {Symbol}. Retrying in {Delay}s...", retryCount, fmpSymbol, timespan.TotalSeconds);
                    });
            #endregion

            try
            {
                return await retryPolicy.ExecuteAsync(async () =>
                {
                    HttpResponseMessage response = await _httpClient.GetAsync(requestUrl, cancellationToken);
                    _ = response.EnsureSuccessStatusCode();
                    List<FmpQuoteDto>? quotes = await response.Content.ReadFromJsonAsync<List<FmpQuoteDto>>(cancellationToken: cancellationToken);
                    if (quotes == null || !quotes.Any())
                    {
                        string rawJson = await response.Content.ReadAsStringAsync(cancellationToken);
                        _logger.LogWarning("FMP API returned a successful (200 OK) response but the data array was null or empty for symbol {Symbol}. Raw JSON: {RawJson}", fmpSymbol, rawJson);
                        return Result<FmpQuoteDto>.Failure($"FMP API returned no quote data for {fmpSymbol}.");
                    }
                    return Result<FmpQuoteDto>.Success(quotes.First());
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An exception occurred while fetching a full quote for {Symbol} from FMP API.", fmpSymbol);
                return Result<FmpQuoteDto>.Failure($"FMP API error: {ex.Message}");
            }
        }
        public async Task<Result<List<FmpQuoteDto>>> GetFullCryptoQuoteListAsync(CancellationToken cancellationToken)
        {
            string requestUrl = $"{BaseUrl}/crypto?apikey={ApiKey}";
            _logger.LogInformation("Requesting all crypto quotes from FMP stable API (paid endpoint).");
            try
            {
                List<FmpQuoteDto>? quotes = await _httpClient.GetFromJsonAsync<List<FmpQuoteDto>>(requestUrl, cancellationToken);
                return quotes == null || !quotes.Any()
                    ? Result<List<FmpQuoteDto>>.Failure("FMP API returned no data or an empty list.")
                    : Result<List<FmpQuoteDto>>.Success(quotes);
            }
            catch (System.Net.Http.HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.PaymentRequired)
            {
                _logger.LogWarning(ex, "FMP API returned '402 Payment Required' for the bulk crypto endpoint. This is a free-tier limitation."); return Result<List<FmpQuoteDto>>.Failure("This fallback data source requires a premium API key.");
            }
            catch (Exception ex) { _logger.LogError(ex, "An exception occurred while fetching the bulk crypto list from FMP API."); return Result<List<FmpQuoteDto>>.Failure($"FMP API error: {ex.Message}"); }
        }
    }
}