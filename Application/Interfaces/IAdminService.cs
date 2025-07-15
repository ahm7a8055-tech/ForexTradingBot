// --- START OF FILE: Application/Interfaces/IAdminService.cs ---

using Application.DTOs.Admin;
using Application.DTOs.Settings; // For the detailed DTO

namespace Application.Interfaces
{
    /// <summary>
    /// Defines a contract for administrative services, such as fetching stats,
    /// user data, and lists for broadcasting.
    /// </summary>
    public interface IAdminService
    {

        Task<(byte[]? ZipContents, string FileName, string? ErrorMessage)> GetLogFilesAsZipAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets dashboard statistics, including total user and news item counts.
        /// </summary>
        Task<(int UserCount, int NewsItemCount)> GetDashboardStatsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves a list of all active user Telegram IDs for broadcasting.
        /// </summary>
        /// <returns>A list of numeric Telegram chat IDs.</returns>
        Task<List<long>> GetAllActiveUserChatIdsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Finds a single user by their unique Telegram ID and compiles a detailed DTO
        /// with related information like subscriptions and transactions.
        /// </summary>
        /// <param name="telegramId">The Telegram ID of the user to look up.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A detailed DTO for the admin panel, or null if the user is not found.</returns>
        Task<AdminUserDetailDto?> GetUserDetailByTelegramIdAsync(long telegramId, CancellationToken cancellationToken = default);

        Task<string> ExecuteRawSqlQueryAsync(string sqlQuery, CancellationToken cancellationToken = default);

        Task<ForceJoinSettingsDto> GetForceJoinSettingsAsync(CancellationToken cancellationToken = default);

        Task UpdateForceJoinSettingsAsync(ForceJoinSettingsDto settings, CancellationToken cancellationToken = default);
        /// <summary>
        /// Gets dashboard statistics, including total user and news item counts, and user join stats for the last 7 days.
        /// </summary>
        Task<(int UserCount, int NewsItemCount, List<(DateTime Date, int Count)> UserJoinStats)> GetDashboardStatsWithUserJoinsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets consolidated statistics for the admin dashboard.
        /// </summary>
        Task<AdminDashboardStatsDto> GetAdminDashboardStatsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Lists available log files.
        /// </summary>
        /// <returns>A list of log file names.</returns>
        Task<List<string>> ListLogFilesAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the content of a specific log file.
        /// </summary>
        /// <param name="fileName">The name of the log file.</param>
        /// <param name="lineCount">Optional: The number of lines from the end of the file to return. If null or 0, returns full content.</param>
        /// <returns>The content of the log file, or null if not found or an error occurs.</returns>
        Task<string?> GetLogFileContentAsync(string fileName, int? lineCount = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the most recent pro monitoring logs for admin/monitoring purposes.
        /// </summary>
        Task<List<Domain.Entities.ProMonitoringLog>> GetRecentProMonitoringLogsAsync(int limit, int offset, CancellationToken cancellationToken = default);


        Task<int> DeleteAllProMonitoringLogsAsync(CancellationToken cancellationToken = default);
    }
}