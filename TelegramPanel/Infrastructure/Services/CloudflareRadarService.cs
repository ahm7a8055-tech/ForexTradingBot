using Application.Interfaces;
using Microsoft.Extensions.Logging;
using Shared.Results;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Infrastructure.Services
{
    /// <summary>
    /// A high-performance, robust service to fetch real-time internet insights from the Cloudflare Radar API.
    /// This implementation uses a correct parallel execution model and the definitive, correct API endpoints.
    /// </summary>
    public class CloudflareRadarService : ICloudflareRadarService
    {
        private readonly ILogger<CloudflareRadarService> _logger;
        private readonly IHttpClientFactory _httpClientFactory;

        private const string ApiToken = "iarYYgFtNTOxBGoPUPd_TYoQ4L5p4xfqOdzKt-pH";
        private const string BaseUrl = "https://api.cloudflare.com/client/v4";

        public CloudflareRadarService(ILogger<CloudflareRadarService> logger, IHttpClientFactory httpClientFactory)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        }

        public async Task<Result<CloudflareCountryReportDto>> GetCountryReportAsync(string countryCode, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Fetching LIVE Cloudflare Radar report for country: {CountryCode}", countryCode);
            if (string.IsNullOrWhiteSpace(countryCode))
                return Result<CloudflareCountryReportDto>.Failure("Country code cannot be empty.");

            try
            {
                var dateRange = "7d";
                var anomaliesDateRange = "7d";

                // --- Start all API calls in parallel using the definitive endpoints ---
                var locationTask = GetFromApiAsync<LocationWrapper>($"/radar/entities/locations/{countryCode.ToUpper()}", cancellationToken);
                var iqiTask = GetFromApiAsync<IqiSummaryPayloadWrapper>($"/radar/quality/iqi/summary?location={countryCode}&dateRange={dateRange}&metric=latency", cancellationToken);
                // DEFINITIVE FIX: Using the correct '/top/locations/origin' endpoint to get top attackers for the target country.
                var attacksTask = GetFromApiAsync<TopAttacksWrapper>($"/radar/attacks/layer7/top/locations/origin?location={countryCode}&dateRange={dateRange}&limit=1", cancellationToken);
                var httpProtocolTask = GetFromApiAsync<HttpProtocolApiResultPayload>($"/radar/http/summary/http_version?location={countryCode}&dateRange={dateRange}", cancellationToken);
                var deviceTypeTask = GetFromApiAsync<DeviceTypeApiResultPayload>($"/radar/http/summary/device_type?location={countryCode}&dateRange={dateRange}", cancellationToken);
                var botHumanTask = GetFromApiAsync<BotTrafficApiResultPayload>($"/radar/http/summary/bot_class?location={countryCode}&dateRange={dateRange}", cancellationToken);
                var trafficAnomaliesTask = GetFromApiAsync<TrafficAnomaliesWrapper>($"/radar/traffic_anomalies?limit=10&dateRange={anomaliesDateRange}", cancellationToken);

                await Task.WhenAll(
                    locationTask, iqiTask, attacksTask,
                    httpProtocolTask, deviceTypeTask, botHumanTask, trafficAnomaliesTask
                );

                var (locationData, _) = locationTask.Result;
                var (iqiData, _) = iqiTask.Result;
                var (topAttackData, _) = attacksTask.Result;
                var (httpProtocolData, httpMeta) = httpProtocolTask.Result;
                var (deviceTypeData, _) = deviceTypeTask.Result;
                var (botHumanData, _) = botHumanTask.Result;
                var (trafficAnomaliesData, _) = trafficAnomaliesTask.Result;

                var latestAnomaly = trafficAnomaliesData?.TrafficAnomalies
                    .FirstOrDefault(a => a.Locations?.Code == countryCode.ToUpper());

                var iqiDto = iqiData?.Summary0 != null ? new IqiData(iqiData.Summary0.P90, iqiData.Summary0.Rating ?? "N/A") : null;

                var topAttack = topAttackData?.Top0.FirstOrDefault();
                double.TryParse(topAttack?.Value.TrimEnd('%'), CultureInfo.InvariantCulture, out var attackPercentage);
                var attackDto = topAttack != null ? new AttackData(topAttack.ClientCountryName, attackPercentage) : null;

                double.TryParse(httpProtocolData?.Summary0?.Http2, CultureInfo.InvariantCulture, out var http2);
                double.TryParse(httpProtocolData?.Summary0?.Http3, CultureInfo.InvariantCulture, out var http3);
                var httpDto = httpProtocolData?.Summary0 != null ? new HttpProtocolData(http2, http3) : null;

                double.TryParse(deviceTypeData?.Summary0?.Desktop, CultureInfo.InvariantCulture, out var desktop);
                double.TryParse(deviceTypeData?.Summary0?.Mobile, CultureInfo.InvariantCulture, out var mobile);
                var deviceDto = deviceTypeData?.Summary0 != null ? new DeviceTypeData(desktop, mobile) : null;

                double.TryParse(botHumanData?.Summary0?.Bot, CultureInfo.InvariantCulture, out var bot);
                double.TryParse(botHumanData?.Summary0?.Human, CultureInfo.InvariantCulture, out var human);
                var botDto = botHumanData?.Summary0 != null ? new BotTrafficData(bot, human) : null;

                var anomalyDto = latestAnomaly != null ? new TrafficAnomalyData(latestAnomaly.Status, latestAnomaly.StartDate) : null;

                var report = new CloudflareCountryReportDto
                {
                    CountryCode = countryCode.ToUpper(),
                    CountryName = locationData?.Location?.Name ?? countryCode.ToUpper(),
                    RadarUrl = $"https://radar.cloudflare.com/locations/{countryCode.ToLower()}",
                    ReportTimestamp = httpMeta?.LastUpdated,
                    InternetQuality = iqiDto,
                    LatestTrafficAnomaly = anomalyDto,
                    Layer7Attacks = attackDto,
                    HttpProtocolDistribution = httpDto,
                    DeviceTypeDistribution = deviceDto,
                    BotVsHumanTraffic = botDto
                };

                _logger.LogInformation("Successfully fetched and compiled live report for {CountryCode}", countryCode);
                return Result<CloudflareCountryReportDto>.Success(report);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while fetching Cloudflare Radar data for {CountryCode}", countryCode);
                return Result<CloudflareCountryReportDto>.Failure($"API call failed: {ex.Message}");
            }
        }

        private async Task<(T? result, Meta? meta)> GetFromApiAsync<T>(string endpoint, CancellationToken cancellationToken) where T : class
        {
            var client = _httpClientFactory.CreateClient("Cloudflare");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ApiToken);
            var url = $"{BaseUrl}{endpoint}";
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var response = await client.GetAsync(url, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogWarning("Cloudflare API call to {Url} failed with status {StatusCode}. Response: {ErrorContent}", url, response.StatusCode, errorContent);
                    return (null, null);
                }
                var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
                var apiResponse = await JsonSerializer.DeserializeAsync<ApiResponse<T>>(contentStream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, cancellationToken);
                if (apiResponse?.Success == false)
                {
                    var errorMessages = string.Join(", ", apiResponse.Errors?.Select(e => e.Message) ?? Enumerable.Empty<string>());
                    _logger.LogWarning("Cloudflare API call to {Url} was successful but returned an error status. Errors: {Errors}", url, errorMessages);
                    return (null, null);
                }
                return (apiResponse?.Result, apiResponse?.Meta);
            }
            catch (OperationCanceledException) { _logger.LogWarning("Cloudflare API call to {Url} was canceled.", url); return (null, null); }
            catch (Exception ex) { _logger.LogError(ex, "Exception during Cloudflare API call to {Url}", url); return (null, null); }
        }

        #region Private API Response Models
        private record ApiResponse<T> { [JsonPropertyName("result")] public T? Result { get; init; } [JsonPropertyName("success")] public bool Success { get; init; } [JsonPropertyName("errors")] public JsonError[]? Errors { get; init; } [JsonPropertyName("meta")] public Meta? Meta { get; init; } }
        private record JsonError(int Code, string Message);
        private record Meta { [JsonPropertyName("last_updated")] public string? LastUpdated { get; init; } }
        private record LocationWrapper { [JsonPropertyName("location")] public LocationPayload? Location { get; init; } }
        private record LocationPayload { [JsonPropertyName("name")] public string Name { get; init; } = ""; }
        private record IqiSummaryPayloadWrapper { [JsonPropertyName("summary_0")] public IqiSummaryPayload? Summary0 { get; init; } }
        private record IqiSummaryPayload { [JsonPropertyName("p90")] public double P90 { get; init; } [JsonPropertyName("rating")] public string? Rating { get; init; } }
        private record TopAttacksWrapper { [JsonPropertyName("top_0")] public List<AttackOriginPayload> Top0 { get; init; } = []; }
        private record AttackOriginPayload { [JsonPropertyName("clientCountryName")] public string ClientCountryName { get; init; } = ""; [JsonPropertyName("value")] public string Value { get; init; } = ""; }
        private record HttpProtocolApiResultPayload { [JsonPropertyName("summary_0")] public HttpProtocolSummaryPayload? Summary0 { get; init; } }
        private record HttpProtocolSummaryPayload { [JsonPropertyName("HTTP/2")] public string Http2 { get; init; } = ""; [JsonPropertyName("HTTP/3")] public string Http3 { get; init; } = ""; }
        private record DeviceTypeApiResultPayload { [JsonPropertyName("summary_0")] public DeviceTypeSummaryPayload? Summary0 { get; init; } }
        private record DeviceTypeSummaryPayload { [JsonPropertyName("desktop")] public string Desktop { get; init; } = ""; [JsonPropertyName("mobile")] public string Mobile { get; init; } = ""; }
        private record BotTrafficApiResultPayload { [JsonPropertyName("summary_0")] public BotTrafficSummaryPayload? Summary0 { get; init; } }
        private record BotTrafficSummaryPayload { [JsonPropertyName("bot")] public string Bot { get; init; } = ""; [JsonPropertyName("human")] public string Human { get; init; } = ""; }
        private record TrafficAnomaliesWrapper { [JsonPropertyName("trafficAnomalies")] public List<TrafficAnomalyPayload> TrafficAnomalies { get; init; } = []; }
        private record TrafficAnomalyPayload { [JsonPropertyName("locations")] public AnomalyLocation? Locations { get; init; } [JsonPropertyName("startDate")] public DateTime StartDate { get; init; } [JsonPropertyName("status")] public string Status { get; init; } = ""; }
        private record AnomalyLocation { [JsonPropertyName("code")] public string Code { get; init; } = ""; }
        #endregion
    }
}