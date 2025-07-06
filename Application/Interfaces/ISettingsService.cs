using Application.DTOs.Settings;
using Application.DTOs.Telegram; // Added for new DTOs
using System.Threading.Tasks;    // Added for Task
using System.Threading;          // Added for CancellationToken

namespace Application.Interfaces
{
    /// <summary>
    /// Defines a contract for retrieving application-wide settings,
    /// typically with a caching layer for performance.
    /// </summary>
    public interface ISettingsService
    {
        /// <summary>
        /// Gets the force join channel settings for the bot.
        /// Implementations should cache this value to avoid frequent database lookups.
        /// </summary>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A DTO containing the force join settings.</returns>
        Task<ForceJoinSettingsDto> GetForceJoinSettingsAsync(CancellationToken cancellationToken = default);

        // New methods for Telegram Bot Settings
        Task<TelegramBotSettingsDto> GetTelegramBotSettingsAsync(CancellationToken cancellationToken = default);
        Task UpdateTelegramBotSettingsAsync(TelegramBotSettingsDto settings, CancellationToken cancellationToken = default);

        // New methods for Telegram Client Settings
        Task<TelegramClientSettingsDto> GetTelegramClientSettingsAsync(CancellationToken cancellationToken = default);
        Task UpdateTelegramClientSettingsAsync(TelegramClientSettingsDto settings, CancellationToken cancellationToken = default);
    }
}