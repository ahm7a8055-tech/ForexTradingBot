// --- File: Infrastructure/Logging/TelegramSinkOptions.cs ---

using System.ComponentModel.DataAnnotations;

namespace TelegramPanel.Infrastructure.Logging
{
    /// <summary>
    /// Configuration options for the Telegram Admin Sink.
    /// </summary>
    public class TelegramSinkOptions
    {
        public const string SectionName = "TelegramPanel";

        [Required(ErrorMessage = "Telegram Bot Token is required.")]
        public string BotToken { get; set; } = string.Empty;

        [Required, MinLength(1, ErrorMessage = "At least one Admin User ID is required.")]
        public List<long> AdminUserIds { get; set; } = new();

        public string? DashboardUrl { get; set; }
        public TimeSpan ThrottlingPeriod { get; set; } = TimeSpan.FromMinutes(5);
        public int ChannelCapacity { get; set; } = 1000; // Max logs to buffer in memory.
        public int BatchSize { get; set; } = 50; // Max logs to send in one consolidated message.
        public TimeSpan BatchInterval { get; set; } = TimeSpan.FromSeconds(30);
        public int RetryCount { get; set; } = 3;
        public TimeSpan RetryBaseDelay { get; set; } = TimeSpan.FromSeconds(2);
    }
}