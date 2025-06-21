// File: Application/Interfaces/ICloudflareRadarService.cs
using Shared.Results; // Or your appropriate namespace for the Result type

namespace Application.Interfaces
{
    #region DTOs (Data Transfer Objects) - Fully Upgraded and Corrected

    // --- Existing DTOs ---
    public record IqiData(double Value, string Rating);
    public record AttackData(string TopSourceCountry, double PercentageOfTotal);
    public record HttpProtocolData(double Http2, double Http3);
    public record DeviceTypeData(double Desktop, double Mobile);
    public record BotTrafficData(double Bot, double Human);
    public record TrafficAnomalyData(string Status, DateTime Timestamp);
    public record ConfirmedOutageData(string Description, DateTime StartDate, DateTime? EndDate, string Cause);
    public record AttackMitigationData(double Waf, double RateLimiting, double BotManagement);
    public record TlsVersionData(double Tls13, double Tls12);

    // --- NEW DTO ADDED ---
    /// <summary>
    /// Represents the distribution of traffic by IP version.
    /// </summary>
    /// <param name="Ipv4">Percentage of traffic using IPv4.</param>
    /// <param name="Ipv6">Percentage of traffic using IPv6.</param>
    public record IpVersionData(double Ipv4, double Ipv6);


    /// <summary>
    /// The main, consolidated report DTO, containing all fetched data points for a country.
    /// This version is enhanced with new security, stability, and modernization metrics.
    /// </summary>
    public record CloudflareCountryReportDto
    {
        public string CountryCode { get; init; } = "";
        public string CountryName { get; init; } = "";
        public string RadarUrl { get; init; } = "";
        public string? ReportTimestamp { get; init; }

        // --- Existing Properties ---
        public IqiData? InternetQuality { get; init; }
        public TrafficAnomalyData? LatestTrafficAnomaly { get; init; }
        public AttackData? Layer7Attacks { get; init; }
        public HttpProtocolData? HttpProtocolDistribution { get; init; }
        public DeviceTypeData? DeviceTypeDistribution { get; init; }
        public BotTrafficData? BotVsHumanTraffic { get; init; }
        public ConfirmedOutageData? LatestOutage { get; init; }
        public AttackMitigationData? AttackMitigation { get; init; }
        public TlsVersionData? TlsVersionDistribution { get; init; }

        // --- NEW PROPERTY ADDED ---
        public IpVersionData? IpVersionDistribution { get; init; }
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
        Task<Result<CloudflareCountryReportDto>> GetCountryReportAsync(string countryCode, CancellationToken cancellationToken);
    }
}