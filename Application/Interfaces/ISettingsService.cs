// --- START OF NEW FILE: Application/Interfaces/ISettingsService.cs ---

using Application.DTOs.Settings;
using System.Threading;
using System.Threading.Tasks;

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
    }
}