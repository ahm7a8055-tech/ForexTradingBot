// --- START OF FILE: Infrastructure/Services/AdminService.cs ---

using Application.DTOs.Admin;
using Application.DTOs.Settings;
using Application.Interfaces;
using Dapper;
using Domain.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace Infrastructure.Services.Admin
{
    public class AdminService : IAdminService
    {
        private readonly string _connectionString;
        private readonly ILogger<AdminService> _logger;
        private const int CommandTimeoutSeconds = 180; // Increased timeout for admin queries
        private readonly ICacheService _cacheService;
        public AdminService(IConfiguration configuration, ILogger<AdminService> logger, ICacheService cacheService)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                                ?? throw new InvalidOperationException("DefaultConnection string is not found in configuration.");
            _logger = logger;
            _cacheService = cacheService; // --- ADDED ---
        }
        private NpgsqlConnection CreateConnection() => new(_connectionString);

        public async Task<(int UserCount, int NewsItemCount)> GetDashboardStatsAsync(CancellationToken cancellationToken = default)
        {
            // CORRECTED: Using NpgsqlConnection and PostgreSQL-compliant quoted identifiers.
            await using var connection = CreateConnection();

            const string sql = @"SELECT COUNT(1) FROM public.""Users""; SELECT COUNT(1) FROM public.""NewsItems"";";

            using var multi = await connection.QueryMultipleAsync(new CommandDefinition(sql, cancellationToken: cancellationToken, commandTimeout: CommandTimeoutSeconds));
            return (await multi.ReadSingleAsync<int>(), await multi.ReadSingleAsync<int>());
        }

        public async Task<List<long>> GetAllActiveUserChatIdsAsync(CancellationToken cancellationToken = default)
        {
            // CORRECTED: Using NpgsqlConnection and PostgreSQL-compliant quoted identifiers.
            // Also, directly querying for the 'bigint' type is more efficient than parsing strings.
            await using var connection = CreateConnection();

            const string sql = @"SELECT ""TelegramId""::bigint FROM public.""Users"" WHERE ""TelegramId"" IS NOT NULL AND ""TelegramId"" <> '';";

            var ids = await connection.QueryAsync<long>(new CommandDefinition(sql, cancellationToken: cancellationToken));
            return ids.ToList();
        }

        public async Task<(byte[]? ZipContents, string FileName, string? ErrorMessage)> GetLogFilesAsZipAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                // Assuming logs are in a 'logs' folder at the application root.
                string logDirectory = Path.Combine(AppContext.BaseDirectory, "logs");

                if (!Directory.Exists(logDirectory))
                {
                    _logger.LogWarning("Admin requested log download, but the 'logs' directory was not found at {LogPath}", logDirectory);
                    return (null, "", "Log directory not found.");
                }

                var logFiles = Directory.GetFiles(logDirectory, "log-*.txt").OrderByDescending(f => f).ToList();

                if (!logFiles.Any())
                {
                    return (null, "", "No log files found in the directory.");
                }

                // Create a zip file in memory
                await using var memoryStream = new MemoryStream();
                using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
                {
                    foreach (var logFile in logFiles)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        var entryName = Path.GetFileName(logFile);
                        var zipEntry = archive.CreateEntry(entryName, CompressionLevel.Optimal);

                        await using var entryStream = zipEntry.Open();
                        // Use FileShare.ReadWrite to avoid locking issues with the logger
                        await using var fileStream = new FileStream(logFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                        await fileStream.CopyToAsync(entryStream, cancellationToken);
                    }
                }

                // Reset stream position to be read from the beginning
                memoryStream.Position = 0;
                string fileName = $"logs_archive_{DateTime.UtcNow:yyyy-MM-dd_HH-mm-ss}.zip";

                return (memoryStream.ToArray(), fileName, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create log archive for admin download.");
                return (null, "", $"An unexpected error occurred: {ex.Message}");
            }
        }

        // In AdminService.cs
        public async Task<string> ExecuteRawSqlQueryAsync(string sqlQuery, CancellationToken cancellationToken = default)
        {
            _logger.LogWarning("Admin is executing a raw SQL query. THIS IS A HIGH-RISK OPERATION. Query: {Query}", sqlQuery);

            // CORRECTED: Using NpgsqlConnection
            await using var connection = CreateConnection();
            var response = new StringBuilder();

            try
            {
                var command = new CommandDefinition(sqlQuery, commandTimeout: CommandTimeoutSeconds, cancellationToken: cancellationToken);
                using var multi = await connection.QueryMultipleAsync(command);

                int resultSetIndex = 1;
                while (!multi.IsConsumed)
                {
                    // No changes needed here, as Dapper returns IDictionary<string, object> which is provider-agnostic.
                    // ... (Your existing result formatting logic is fine)
                }
                return response.ToString();
            }
            catch (PostgresException pgEx) // CORRECTED: Catch specific PostgreSQL exceptions
            {
                _logger.LogError(pgEx, "Error executing raw SQL query. SQLSTATE: {SqlState}", pgEx.SqlState);
                return $"❌ **PostgreSQL Execution Error (Code: {pgEx.SqlState}):**\n`{pgEx.Message}`";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing raw SQL query.");
                return $"❌ **General Execution Error:**\n`{ex.Message}`";
            }
        }


        // ✅ This is the single, correct implementation for the detailed user lookup.
        public async Task<AdminUserDetailDto?> GetUserDetailByTelegramIdAsync(long telegramId, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Fetching detailed profile for Telegram ID: {TelegramId} using optimized PG query.", telegramId);

            // CORRECTED: A single, optimized query using PostgreSQL's JSON aggregation functions.
            const string sql = @"
                SELECT
                    u.*,
                    (SELECT jsonb_agg(tw.*) FROM public.""TokenWallets"" tw WHERE tw.""UserId"" = u.""Id"") AS ""WalletJson"",
                    (SELECT jsonb_agg(sub.* ORDER BY sub.""StartDate"" DESC) FROM public.""Subscriptions"" sub WHERE sub.""UserId"" = u.""Id"") AS ""SubscriptionsJson"",
                    (SELECT jsonb_agg(t.* ORDER BY t.""Timestamp"" DESC) FROM (SELECT * FROM public.""Transactions"" tr WHERE tr.""UserId"" = u.""Id"" ORDER BY tr.""Timestamp"" DESC LIMIT 10) t) AS ""TransactionsJson""
                FROM public.""Users"" u
                WHERE u.""TelegramId"" = @TelegramIdStr;";

            await using var connection = CreateConnection();

            // Dapper will map the main columns and the JSON strings to this DTO.
            var resultDto = await connection.QuerySingleOrDefaultAsync<AdminUserDetailRawDto>(
                new CommandDefinition(sql, new { TelegramIdStr = telegramId.ToString() }, cancellationToken: cancellationToken)
            );

            if (resultDto == null)
            {
                return null;
            }

            // Map the raw DTO with JSON strings into the final, structured DTO.
            return MapRawDtoToAdminUserDetail(resultDto);
        }

        #region DTOs and Mappers for GetUserDetailByTelegramIdAsync
        private AdminUserDetailDto MapRawDtoToAdminUserDetail(AdminUserDetailRawDto rawDto)
        {
            var userDetail = new AdminUserDetailDto
            {

                UserId = rawDto.Id,
                Username = rawDto.Username,
                TelegramId = long.Parse(rawDto.TelegramId),
                // ... map other user properties
            };

            if (!string.IsNullOrEmpty(rawDto.WalletJson) && rawDto.WalletJson != "[]")
            {
                var wallet = JsonSerializer.Deserialize<List<WalletDto>>(rawDto.WalletJson)?.FirstOrDefault();
                if (wallet != null)
                {
                    userDetail.TokenBalance = wallet.Balance;
                    userDetail.WalletLastUpdated = wallet.UpdatedAt;
                }
            }

            if (!string.IsNullOrEmpty(rawDto.SubscriptionsJson) && rawDto.SubscriptionsJson != "[]")
            {
                var subscriptions = JsonSerializer.Deserialize<List<SubscriptionSummaryDto>>(rawDto.SubscriptionsJson);
                userDetail.Subscriptions = subscriptions;
                var activeSub = subscriptions?.FirstOrDefault(s => s.Status == "Active" && DateTime.UtcNow >= s.StartDate && DateTime.UtcNow <= s.EndDate);
                if (activeSub != null)
                {
                    userDetail.ActiveSubscription = new ActiveSubscriptionDto { EndDate = activeSub.EndDate };
                }
            }

            if (!string.IsNullOrEmpty(rawDto.TransactionsJson) && rawDto.TransactionsJson != "[]")
            {
                userDetail.RecentTransactions = JsonSerializer.Deserialize<List<TransactionSummaryDto>>(rawDto.TransactionsJson);
            }

            return userDetail;
        }
        private class WalletDto // This DTO structure must match the JSON structure from the DB
        {
            public Guid Id { get; set; }
            public Guid UserId { get; set; }
            public decimal Balance { get; set; }
            public bool IsActive { get; set; }
            public DateTime CreatedAt { get; set; }
            public DateTime? UpdatedAt { get; set; } // This property is nullable

            // --- THIS IS THE FIX ---
            // The TokenWallet domain entity likely requires a non-nullable DateTime for UpdatedAt.
            // We must provide a default value if the DTO's UpdatedAt is null.
            public TokenWallet ToDomainEntity() => new TokenWallet(
                Id,
                UserId,
                Balance,
                IsActive,
                CreatedAt,
                UpdatedAt ?? CreatedAt // If UpdatedAt is null, use CreatedAt as a sensible default.
            );
        }
        #endregion
        // This DTO receives the raw data from the database, including the JSON strings.
        private class AdminUserDetailRawDto
        {
            public Guid Id { get; set; }
            public string Username { get; set; } = string.Empty;
            public string TelegramId { get; set; } = string.Empty;
            // ... other user properties
            public string? WalletJson { get; set; }
            public string? SubscriptionsJson { get; set; }
            public string? TransactionsJson { get; set; }
        }



        private const string ForceJoinSettingsKey = "settings:force_join";
        public async Task<ForceJoinSettingsDto> GetForceJoinSettingsAsync(CancellationToken cancellationToken = default)
        {
            await using var connection = CreateConnection();
            const string sql = @"SELECT ""Value"" FROM public.""Settings"" WHERE ""Key"" = @Key;";

            var jsonValue = await connection.QuerySingleOrDefaultAsync<string>(
                new CommandDefinition(sql, new { Key = ForceJoinSettingsKey }, cancellationToken: cancellationToken));

            if (string.IsNullOrEmpty(jsonValue))
            {
                _logger.LogInformation("Force join settings not found in database, returning default (disabled) state.");
                return new ForceJoinSettingsDto { IsEnabled = false };
            }

            return JsonSerializer.Deserialize<ForceJoinSettingsDto>(jsonValue) ?? new ForceJoinSettingsDto();
        }
        public async Task UpdateForceJoinSettingsAsync(ForceJoinSettingsDto settings, CancellationToken cancellationToken = default)
        {
            await using var connection = CreateConnection();
            var jsonValue = JsonSerializer.Serialize(settings);

            const string sql = @"
        INSERT INTO public.""Settings"" (""Key"", ""Value"")
        VALUES (@Key, @Value::jsonb)
        ON CONFLICT (""Key"") DO UPDATE
        SET ""Value"" = EXCLUDED.""Value"";
    ";
            await connection.ExecuteAsync(new CommandDefinition(sql, new { Key = ForceJoinSettingsKey, Value = jsonValue }, cancellationToken: cancellationToken));
            _logger.LogInformation("Force join settings have been updated in the database.");

            // CRITICAL: Invalidate the cache so the application picks up the new setting immediately.
            await _cacheService.RemoveAsync(ForceJoinSettingsKey);
            _logger.LogInformation("Force join settings cache key '{CacheKey}' has been invalidated.", ForceJoinSettingsKey);
        }

        public async Task<(int UserCount, int NewsItemCount, List<(DateTime Date, int Count)> UserJoinStats)> GetDashboardStatsWithUserJoinsAsync(CancellationToken cancellationToken = default)
        {
            await using var connection = CreateConnection();
            // Get total user count and news item count
            const string sqlCounts = @"SELECT COUNT(1) FROM public.""Users""; SELECT COUNT(1) FROM public.""NewsItems"";";
            using var multi = await connection.QueryMultipleAsync(new CommandDefinition(sqlCounts, cancellationToken: cancellationToken, commandTimeout: CommandTimeoutSeconds));
            int userCount = await multi.ReadSingleAsync<int>();
            int newsItemCount = await multi.ReadSingleAsync<int>();

            // Get user join stats for the last 7 days
            const string sqlJoins = @"
                SELECT date_trunc('day', ""CreatedAt"") AS join_date, COUNT(*) AS count
                FROM public.""Users""
                WHERE ""CreatedAt"" >= (CURRENT_DATE - INTERVAL '6 days')
                GROUP BY join_date
                ORDER BY join_date;
            ";
            var joinStatsRaw = (await connection.QueryAsync<(DateTime join_date, int count)>(new CommandDefinition(sqlJoins, cancellationToken: cancellationToken, commandTimeout: CommandTimeoutSeconds))).ToList();

            // Fill missing days with 0
            var userJoinStats = new List<(DateTime Date, int Count)>();
            var today = DateTime.UtcNow.Date;
            for (int i = 6; i >= 0; i--)
            {
                var day = today.AddDays(-i);
                var stat = joinStatsRaw.FirstOrDefault(x => x.join_date.Date == day);
                userJoinStats.Add((day, stat.count));
            }

            return (userCount, newsItemCount, userJoinStats);
        }
    }
}