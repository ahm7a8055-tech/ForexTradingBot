// File: Application/Interfaces/ICloudflareRadarService.cs
using Shared.Results;

namespace Application.Interfaces
{
    #region DTOs (Data Transfer Objects) - Ultimate Version

    public record IqiData(double Value, string Rating);
    public record AttackData(string TopSourceCountry, double PercentageOfTotal);
    public record HttpProtocolData(double Http3, double Http2, double Http1);
    public record DeviceTypeData(double Desktop, double Mobile);
    public record BotTrafficData(double Bot, double Human);
    public record TrafficAnomalyData(string Status, DateTime Timestamp);
    public record ConfirmedOutageData(string Description, DateTime StartDate, DateTime? EndDate, string Cause);
    public record AttackMitigationData(double Waf, double RateLimiting, double BotManagement);
    public record TlsVersionData(double Tls13, double Tls12, double Tls11, double Tls10);
    public record IpVersionData(double Ipv4, double Ipv6);

    // --- NEW DTOS FOR MAXIMUM DETAIL ---
    public record PostQuantumData(double Supported, double NotSupported);
    public record OperatingSystemData(double Windows, double MacOS, double Android, double IOS, double Linux);
    public record Layer3AttackProtocolData(double Udp, double Tcp, double Icmp);

    /// <summary>
    /// The ultimate, consolidated report DTO, containing all fetched data points for a country.
    /// </summary>
    public record CloudflareCountryReportDto
    {
        public string CountryCode { get; init; } = "";
        public string CountryName { get; init; } = "";
        public string RadarUrl { get; init; } = "";
        public string? ReportTimestamp { get; init; }

        public IqiData? InternetQuality { get; init; }
        public ConfirmedOutageData? LatestOutage { get; init; }
        public BotTrafficData? BotVsHumanTraffic { get; init; }
        public AttackData? Layer7Attacks { get; init; }
        public HttpProtocolData? HttpProtocolDistribution { get; init; }
        public DeviceTypeData? DeviceTypeDistribution { get; init; }
        public AttackMitigationData? AttackMitigation { get; init; }
        public TlsVersionData? TlsVersionDistribution { get; init; }
        public IpVersionData? IpVersionDistribution { get; init; }

        // --- NEW PROPERTIES FOR MAXIMUM DETAIL ---
        public PostQuantumData? PostQuantumSupport { get; init; }
        public OperatingSystemData? OSDistribution { get; init; }
        public Layer3AttackProtocolData? L3AttackDistribution { get; init; }
    }
    #endregion

    public interface ICloudflareRadarService
    {
        Task<Result<CloudflareCountryReportDto>> GetCountryReportAsync(string countryCode, CancellationToken cancellationToken);
    }
}