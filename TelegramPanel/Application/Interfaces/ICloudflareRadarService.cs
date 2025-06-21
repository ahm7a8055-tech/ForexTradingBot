// File: Application/Interfaces/ICloudflareRadarService.cs
using Shared.Results; // Or your appropriate namespace for the Result type

namespace Application.Interfaces
{
    #region DTOs (Data Transfer Objects) - Fully Upgraded and Corrected

    public record IqiData(double Value, string Rating);
    public record AttackData(string TopSourceCountry, double PercentageOfTotal);
    public record HttpProtocolData(double Http2, double Http3);
    public record DeviceTypeData(double Desktop, double Mobile);
    public record BotTrafficData(double Bot, double Human);
    public record TrafficAnomalyData(string Status, DateTime Timestamp);

    /// <summary>
    /// The main, consolidated report DTO, containing all fetched data points for a country.
    /// </summary>
    public record CloudflareCountryReportDto
    {
        public string CountryCode { get; init; } = "";
        public string CountryName { get; init; } = "";
        public string RadarUrl { get; init; } = "";
        public string? ReportTimestamp { get; init; }

        public IqiData? InternetQuality { get; init; }
        public TrafficAnomalyData? LatestTrafficAnomaly { get; init; }
        public AttackData? Layer7Attacks { get; init; }
        public HttpProtocolData? HttpProtocolDistribution { get; init; }
        public DeviceTypeData? DeviceTypeDistribution { get; init; }

        // CORRECTED: The type name is now 'BotTrafficData', matching its definition above.
        public BotTrafficData? BotVsHumanTraffic { get; init; }
    }
    #endregion

    /// <summary>
    /// Defines the contract for a service that fetches and consolidates
    /// internet health and security data from Cloudflare's Radar.
    /// </summary>
    public interface ICloudflareRadarService
    {
        /// <summary>
        /// Asynchronously fetches a comprehensive report for a specific country.
        /// </summary>
        /// <param name="countryCode">The ISO 3166-1 Alpha-2 code for the country (e.g., "US", "DE").</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>
        /// A task that represents the asynchronous operation. The task result contains
        /// a Result object which, on success, holds a <see cref="CloudflareCountryReportDto"/>.
        /// </returns>
        Task<Result<CloudflareCountryReportDto>> GetCountryReportAsync(string countryCode, CancellationToken cancellationToken);
    }
}