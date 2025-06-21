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
    /// This definitive version includes powerful JSON logging for complete transparency on every API call.
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
            _logger.LogInformation("Fetching MAXIMUM DETAIL Cloudflare Radar report for country: {CountryCode}", countryCode);
            if (string.IsNullOrWhiteSpace(countryCode))
                return Result<CloudflareCountryReportDto>.Failure("Country code cannot be empty.");

            try
            {
                var dateRange = "7d";
                var countryCodeUpper = countryCode.ToUpper();

                var locationTask = GetFromApiAsync<LocationWrapper>($"/radar/entities/locations/{countryCodeUpper}", cancellationToken);
                var iqiTask = GetFromApiAsync<IqiSummaryPayloadWrapper>($"/radar/quality/iqi/summary?location={countryCodeUpper}&dateRange={dateRange}&metric=latency", cancellationToken);
                var attacksTask = GetFromApiAsync<TopAttacksWrapper>($"/radar/attacks/layer7/top/locations/origin?location={countryCodeUpper}&dateRange={dateRange}&limit=1", cancellationToken);
                var httpProtocolTask = GetFromApiAsync<HttpProtocolApiResultPayload>($"/radar/http/summary/http_version?location={countryCodeUpper}&dateRange={dateRange}", cancellationToken);
                var deviceTypeTask = GetFromApiAsync<DeviceTypeApiResultPayload>($"/radar/http/summary/device_type?location={countryCodeUpper}&dateRange={dateRange}", cancellationToken);
                var botHumanTask = GetFromApiAsync<BotTrafficApiResultPayload>($"/radar/http/summary/bot_class?location={countryCodeUpper}&dateRange={dateRange}", cancellationToken);
                var outagesTask = GetFromApiAsync<OutagesWrapper>($"/radar/annotations/outages?limit=5&dateRange={dateRange}&location={countryCodeUpper}", cancellationToken);
                var mitigationTask = GetFromApiAsync<AttackMitigationPayload>($"/radar/attacks/layer7/summary/mitigation_product?location={countryCodeUpper}&dateRange={dateRange}", cancellationToken);
                var tlsVersionTask = GetFromApiAsync<TlsVersionPayload>($"/radar/http/summary/tls_version?location={countryCodeUpper}&dateRange={dateRange}", cancellationToken);

                // --- NEW API CALL ADDED ---
                var ipVersionTask = GetFromApiAsync<IpVersionPayload>($"/radar/http/summary/ip_version?location={countryCodeUpper}&dateRange={dateRange}", cancellationToken);

                await Task.WhenAll(
                    locationTask, iqiTask, attacksTask,
                    httpProtocolTask, deviceTypeTask, botHumanTask, outagesTask,
                    mitigationTask, tlsVersionTask, ipVersionTask // Await new task
                );

                var (locationData, _) = locationTask.Result;
                var (iqiData, _) = iqiTask.Result;
                var (topAttackData, _) = attacksTask.Result;
                var (httpProtocolData, httpMeta) = httpProtocolTask.Result;
                var (deviceTypeData, _) = deviceTypeTask.Result;
                var (botHumanData, _) = botHumanTask.Result;
                var (outagesData, _) = outagesTask.Result;
                var (mitigationData, _) = mitigationTask.Result;
                var (tlsData, _) = tlsVersionTask.Result;
                var (ipVersionData, _) = ipVersionTask.Result;

                var latestOutage = outagesData?.Annotations.FirstOrDefault();
                var outageDto = latestOutage != null ? new ConfirmedOutageData(latestOutage.Description, latestOutage.StartDate, latestOutage.EndDate, latestOutage.Outage?.OutageCause ?? "N/A") : null;

                double.TryParse(mitigationData?.Summary0?.Waf, CultureInfo.InvariantCulture, out var waf);
                double.TryParse(mitigationData?.Summary0?.RateLimiting, CultureInfo.InvariantCulture, out var rateLimit);
                double.TryParse(mitigationData?.Summary0?.BotManagement, CultureInfo.InvariantCulture, out var botMgmt);
                var mitigationDto = mitigationData?.Summary0 != null ? new AttackMitigationData(waf, rateLimit, botMgmt) : null;

                double.TryParse(tlsData?.Summary0?.Tls13, CultureInfo.InvariantCulture, out var tls13);
                double.TryParse(tlsData?.Summary0?.Tls12, CultureInfo.InvariantCulture, out var tls12);
                var tlsDto = tlsData?.Summary0 != null ? new TlsVersionData(tls13, tls12) : null;

                // --- NEW DATA PROCESSING ---
                double.TryParse(ipVersionData?.Summary0?.Ipv4, CultureInfo.InvariantCulture, out var ipv4);
                double.TryParse(ipVersionData?.Summary0?.Ipv6, CultureInfo.InvariantCulture, out var ipv6);
                var ipDto = ipVersionData?.Summary0 != null ? new IpVersionData(ipv4, ipv6) : null;

                var iqiDto = iqiData?.Summary0 != null ? new IqiData(iqiData.Summary0.P90, iqiData.Summary0.Rating ?? "N/A") : null;
                var topAttack = topAttackData?.Top0.FirstOrDefault();
                double.TryParse(topAttack?.Value.TrimEnd('%'), CultureInfo.InvariantCulture, out var attackPercentage);
                // BUG FIX: Provide a default value if the country name is null/empty
                var attackDto = topAttack != null ? new AttackData(string.IsNullOrWhiteSpace(topAttack.ClientCountryName) ? "Unknown" : topAttack.ClientCountryName, attackPercentage) : null;

                double.TryParse(httpProtocolData?.Summary0?.Http2, CultureInfo.InvariantCulture, out var http2);
                double.TryParse(httpProtocolData?.Summary0?.Http3, CultureInfo.InvariantCulture, out var http3);
                var httpDto = httpProtocolData?.Summary0 != null ? new HttpProtocolData(http2, http3) : null;

                double.TryParse(deviceTypeData?.Summary0?.Desktop, CultureInfo.InvariantCulture, out var desktop);
                double.TryParse(deviceTypeData?.Summary0?.Mobile, CultureInfo.InvariantCulture, out var mobile);
                var deviceDto = deviceTypeData?.Summary0 != null ? new DeviceTypeData(desktop, mobile) : null;

                double.TryParse(botHumanData?.Summary0?.Bot, CultureInfo.InvariantCulture, out var bot);
                double.TryParse(botHumanData?.Summary0?.Human, CultureInfo.InvariantCulture, out var human);
                var botDto = botHumanData?.Summary0 != null ? new BotTrafficData(bot, human) : null;

                var report = new CloudflareCountryReportDto
                {
                    CountryCode = countryCodeUpper,
                    CountryName = locationData?.Location?.Name ?? countryCodeUpper,
                    RadarUrl = $"https://radar.cloudflare.com/locations/{countryCode.ToLower()}",
                    ReportTimestamp = httpMeta?.LastUpdated,
                    InternetQuality = iqiDto,
                    Layer7Attacks = attackDto,
                    HttpProtocolDistribution = httpDto,
                    DeviceTypeDistribution = deviceDto,
                    BotVsHumanTraffic = botDto,
                    LatestOutage = outageDto,
                    AttackMitigation = mitigationDto,
                    TlsVersionDistribution = tlsDto,
                    // --- POPULATE NEW DTO PROPERTY ---
                    IpVersionDistribution = ipDto
                };

                _logger.LogInformation("Successfully fetched and compiled MAXIMUM DETAIL live report for {CountryCode}", countryCode);
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
                var rawJson = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogDebug("RAW RESPONSE from {Url}: {RawJson}", url, rawJson);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Cloudflare API call to {Url} failed with status {StatusCode}. Response: {ErrorContent}", url, response.StatusCode, rawJson);
                    return (null, null);
                }

                var apiResponse = JsonSerializer.Deserialize<ApiResponse<T>>(rawJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
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
        private record OutagesWrapper { [JsonPropertyName("annotations")] public List<OutageAnnotationPayload> Annotations { get; init; } = []; }
        private record OutageAnnotationPayload { [JsonPropertyName("description")] public string Description { get; init; } = ""; [JsonPropertyName("startDate")] public DateTime StartDate { get; init; } [JsonPropertyName("endDate")] public DateTime? EndDate { get; init; } [JsonPropertyName("outage")] public OutageDetails? Outage { get; init; } }
        private record OutageDetails { [JsonPropertyName("outageCause")] public string OutageCause { get; init; } = ""; }
        private record AttackMitigationPayload { [JsonPropertyName("summary_0")] public MitigationSummary? Summary0 { get; init; } }
        private record MitigationSummary { [JsonPropertyName("WAF")] public string Waf { get; init; } = ""; [JsonPropertyName("RATE_LIMITING")] public string RateLimiting { get; init; } = ""; [JsonPropertyName("BOT_MANAGEMENT")] public string BotManagement { get; init; } = ""; }
        private record TlsVersionPayload { [JsonPropertyName("summary_0")] public TlsSummary? Summary0 { get; init; } }
        private record TlsSummary { [JsonPropertyName("TLSv1.3")] public string Tls13 { get; init; } = ""; [JsonPropertyName("TLSv1.2")] public string Tls12 { get; init; } = ""; }

        // --- NEW API MODEL ---
        private record IpVersionPayload { [JsonPropertyName("summary_0")] public IpVersionSummary? Summary0 { get; init; } }
        private record IpVersionSummary { [JsonPropertyName("IPv4")] public string Ipv4 { get; init; } = ""; [JsonPropertyName("IPv6")] public string Ipv6 { get; init; } = ""; }
        #endregion
    }
}