// --- START OF FILE: Infrastructure/Services/AdminService.cs ---

#region Usings
using Application.DTOs.Admin;
using Application.DTOs.Settings;
using Application.Interfaces;
using Application.Common.Interfaces;
using Dapper;
using Domain.Entities;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using Shared.Security; // For SecureExceptionSanitizer
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
#endregion

namespace Infrastructure.Services.Admin
{
    /// <summary>
    /// Service for administrative operations with secure logging and data handling.
    /// Provides dashboard statistics, user management, and system administration features.
    /// </summary>
    public class AdminService : IAdminService
    {
        private readonly string _connectionString;
        private readonly ILogger<AdminService> _logger;
        private const int CommandTimeoutSeconds = 180; // Increased timeout for admin queries
        private readonly ICacheService _cacheService;
        private readonly IUserRepository _userRepository;
        private readonly ISignalRepository _signalRepository;

        public AdminService(
            IConfiguration configuration,
            ILogger<AdminService> logger,
            ICacheService cacheService,
            IUserRepository userRepository,
            ISignalRepository signalRepository)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection") ?? throw new ArgumentNullException(nameof(configuration), "DefaultConnection string not found");
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
            _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
            _signalRepository = signalRepository ?? throw new ArgumentNullException(nameof(signalRepository));
        }

        #region Security Methods
        /// <summary>
        /// Sanitizes input for safe logging by removing newlines and other problematic characters.
        /// </summary>
        /// <param name="input">The input to sanitize</param>
        /// <returns>Sanitized string safe for logging</returns>
        private static string SanitizeForLogging(string? input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return "[EMPTY_INPUT]";

            // Remove newlines, carriage returns, and other problematic characters
            var sanitized = input
                .Replace("\r", "")
                .Replace("\n", "")
                .Replace("\t", " ")
                .Replace("\0", ""); // Null characters

            // Remove any remaining control characters
            sanitized = System.Text.RegularExpressions.Regex.Replace(sanitized, @"[\x00-\x1F\x7F]", "");

            // Limit length to prevent log flooding
            if (sanitized.Length > 200)
            {
                sanitized = sanitized[..200] + "...";
            }

            return sanitized;
        }

        /// <summary>
        /// Sanitizes sensitive data by redacting sensitive patterns while preserving structure.
        /// </summary>
        /// <param name="sensitiveInput">The sensitive input to sanitize</param>
        /// <returns>Sanitized string with sensitive data redacted</returns>
        private static string SanitizeSensitiveData(string? sensitiveInput)
        {
            if (string.IsNullOrWhiteSpace(sensitiveInput))
                return "[EMPTY_INPUT]";

            var sanitized = sensitiveInput;

            // Redact connection string patterns
            sanitized = System.Text.RegularExpressions.Regex.Replace(sanitized, 
                @"(?:password|pwd)\s*=\s*[^;\s]+", "password=***", 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            
            sanitized = System.Text.RegularExpressions.Regex.Replace(sanitized, 
                @"(?:user\s*id|uid|username)\s*=\s*[^;\s]+", "userid=***", 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            // Redact bot token patterns
            sanitized = System.Text.RegularExpressions.Regex.Replace(sanitized, 
                @"[0-9]+:[a-zA-Z0-9\-_]{35}", "***:***", 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            // Redact API keys and tokens
            sanitized = System.Text.RegularExpressions.Regex.Replace(sanitized, 
                @"[a-zA-Z0-9\-_]{20,}", "***", 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            return sanitized;
        }

        /// <summary>
        /// Validates file name to prevent path traversal attacks.
        /// </summary>
        /// <param name="fileName">The file name to validate</param>
        /// <returns>Validated file name or null if invalid</returns>
        private string? ValidateFileName(string? fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return null;

            // SECURITY: Sanitize input before validation
            var sanitizedFileName = SanitizeForLogging(fileName);
            
            // Check for path traversal attempts
            if (fileName.Contains("..") || fileName.Contains("\\") || fileName.Contains("/"))
            {
                _logger.LogWarning("Path traversal attempt detected in file name: {SanitizedFileName}", sanitizedFileName);
                return null;
            }

            // Validate log file format (log-YYYYMMDD.txt)
            if (!System.Text.RegularExpressions.Regex.IsMatch(fileName, @"^log-\d{8}\.txt$"))
            {
                _logger.LogWarning("Invalid log file name format: {SanitizedFileName}. Expected format: log-YYYYMMDD.txt", sanitizedFileName);
                return null;
            }

            return fileName;
        }

        /// <summary>
        /// Creates a database connection with proper error handling.
        /// </summary>
        /// <returns>NpgsqlConnection instance</returns>
        private NpgsqlConnection CreateConnection()
        {
            return new NpgsqlConnection(_connectionString);
        }
        #endregion

        public async Task<(int UserCount, int NewsItemCount)> GetDashboardStatsAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Fetching dashboard statistics.");
            try
            {
                await using NpgsqlConnection connection = CreateConnection();
                const string sql = @"
                    SELECT 
                        (SELECT COUNT(*) FROM public.""Users"") as UserCount,
                        (SELECT COUNT(*) FROM public.""NewsItems"") as NewsItemCount;";

                var result = await connection.QuerySingleAsync<(int UserCount, int NewsItemCount)>(
                    new CommandDefinition(sql, cancellationToken: cancellationToken));

                _logger.LogInformation("Dashboard statistics retrieved successfully. Users: {UserCount}, News Items: {NewsItemCount}", 
                    result.UserCount, result.NewsItemCount);

                return result;
            }
            catch (Exception ex)
            {
                // SECURITY: Use SecureExceptionSanitizer for logging exceptions
                var sanitizedException = SecureExceptionSanitizer.SanitizeForLogging(ex);
                _logger.LogError(sanitizedException, "Error fetching dashboard statistics.");
                throw;
            }
        }

        public async Task<List<long>> GetAllActiveUserChatIdsAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Fetching all active user chat IDs.");
            try
            {
                await using NpgsqlConnection connection = CreateConnection();
                const string sql = @"SELECT ""TelegramId"" FROM public.""Users"" WHERE ""EnableGeneralNotifications"" = true;";

                var telegramIds = await connection.QueryAsync<string>(
                    new CommandDefinition(sql, cancellationToken: cancellationToken));

                var result = telegramIds.Select(id => long.Parse(id)).ToList();
                _logger.LogInformation("Retrieved {Count} active user chat IDs.", result.Count);
                return result;
            }
            catch (Exception ex)
            {
                // SECURITY: Use SecureExceptionSanitizer for logging exceptions
                var sanitizedException = SecureExceptionSanitizer.SanitizeForLogging(ex);
                _logger.LogError(sanitizedException, "Error fetching active user chat IDs.");
                throw;
            }
        }

        public async Task<(byte[]? ZipContents, string FileName, string? ErrorMessage)> GetLogFilesAsZipAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Creating log files ZIP archive.");
            try
            {
                var logFiles = await ListLogFilesAsync(cancellationToken);
                if (!logFiles.Any())
                {
                    return (null, "", "No log files found.");
                }

                using var memoryStream = new MemoryStream();
                using var archive = new System.IO.Compression.ZipArchive(memoryStream, System.IO.Compression.ZipArchiveMode.Create, true);

                foreach (var logFileName in logFiles)
                {
                    // SECURITY: Validate file name before processing
                    var validatedFileName = ValidateFileName(logFileName);
                    if (validatedFileName == null)
                    {
                        _logger.LogWarning("Skipping invalid log file name: {SanitizedFileName}", SanitizeForLogging(logFileName));
                        continue;
                    }

                    var logContent = await GetLogFileContentAsync(validatedFileName, null, cancellationToken);
                    if (!string.IsNullOrEmpty(logContent))
                    {
                        var entry = archive.CreateEntry(validatedFileName);
                        using var entryStream = entry.Open();
                        using var writer = new StreamWriter(entryStream);
                        await writer.WriteAsync(logContent);
                    }
                }

                var zipFileName = $"logs_{DateTime.UtcNow:yyyyMMdd_HHmmss}.zip";
                return (memoryStream.ToArray(), zipFileName, null);
            }
            catch (Exception ex)
            {
                // SECURITY: Use SecureExceptionSanitizer for logging exceptions
                var sanitizedException = SecureExceptionSanitizer.SanitizeForLogging(ex);
                _logger.LogError(sanitizedException, "Failed to create log archive for admin download.");
                return (null, "", $"An unexpected error occurred: {ex.Message}");
            }
        }

        // In AdminService.cs
        public async Task<string> ExecuteRawSqlQueryAsync(string sqlQuery, CancellationToken cancellationToken = default)
        {
            // SECURITY: Sanitize SQL query before logging to prevent log forging
            var sanitizedQuery = SanitizeForLogging(sqlQuery);
            _logger.LogWarning("Admin is executing a raw SQL query. THIS IS A HIGH-RISK OPERATION. Query: {SanitizedQuery}", sanitizedQuery);

            // CORRECTED: Using NpgsqlConnection
            await using NpgsqlConnection connection = CreateConnection();
            StringBuilder response = new();

            try
            {
                CommandDefinition command = new(sqlQuery, commandTimeout: CommandTimeoutSeconds, cancellationToken: cancellationToken);
                using SqlMapper.GridReader multi = await connection.QueryMultipleAsync(command);
                while (!multi.IsConsumed)
                {
                    // No changes needed here, as Dapper returns IDictionary<string, object> which is provider-agnostic.
                    // ... (Your existing result formatting logic is fine)
                }
                return response.ToString();
            }
            catch (PostgresException pgEx) // CORRECTED: Catch specific PostgreSQL exceptions
            {
                // SECURITY: Use SecureExceptionSanitizer for logging exceptions
                var sanitizedException = SecureExceptionSanitizer.SanitizeForLogging(pgEx);
                _logger.LogError(sanitizedException, "Error executing raw SQL query. SQLSTATE: {SqlState}", pgEx.SqlState);
                return $"❌ **PostgreSQL Execution Error (Code: {pgEx.SqlState}):**\n`{pgEx.Message}`";
            }
            catch (Exception ex)
            {
                // SECURITY: Use SecureExceptionSanitizer for logging exceptions
                var sanitizedException = SecureExceptionSanitizer.SanitizeForLogging(ex);
                _logger.LogError(sanitizedException, "Error executing raw SQL query.");
                return $"❌ **General Execution Error:**\n`{ex.Message}`";
            }
        }

        // ✅ This is the single, correct implementation for the detailed user lookup.
        public async Task<AdminUserDetailDto?> GetUserDetailByTelegramIdAsync(long telegramId, CancellationToken cancellationToken = default)
        {
            // SECURITY: Sanitize telegramId before logging (though it's a long, it's still user input)
            var sanitizedTelegramId = SanitizeForLogging(telegramId.ToString());
            _logger.LogInformation("Fetching detailed profile for Telegram ID: {SanitizedTelegramId} using optimized PG query.", sanitizedTelegramId);

            // CORRECTED: A single, optimized query using PostgreSQL's JSON aggregation functions.
            const string sql = @"
                SELECT
                    u.*,
                    (SELECT jsonb_agg(tw.*) FROM public.""TokenWallets"" tw WHERE tw.""UserId"" = u.""Id"") AS ""WalletJson"",
                    (SELECT jsonb_agg(sub.* ORDER BY sub.""StartDate"" DESC) FROM public.""Subscriptions"" sub WHERE sub.""UserId"" = u.""Id"") AS ""SubscriptionsJson"",
                    (SELECT jsonb_agg(t.* ORDER BY t.""Timestamp"" DESC) FROM (SELECT * FROM public.""Transactions"" tr WHERE tr.""UserId"" = u.""Id"" ORDER BY tr.""Timestamp"" DESC LIMIT 10) t) AS ""TransactionsJson""
                FROM public.""Users"" u
                WHERE u.""TelegramId"" = @TelegramIdStr;";

            await using NpgsqlConnection connection = CreateConnection();

            // Dapper will map the main columns and the JSON strings to this DTO.
            AdminUserDetailRawDto? resultDto = await connection.QuerySingleOrDefaultAsync<AdminUserDetailRawDto>(
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
            AdminUserDetailDto userDetail = new()
            {

                UserId = rawDto.Id,
                Username = rawDto.Username,
                TelegramId = long.Parse(rawDto.TelegramId),
                // ... map other user properties
            };

            if (!string.IsNullOrEmpty(rawDto.WalletJson) && rawDto.WalletJson != "[]")
            {
                WalletDto? wallet = JsonSerializer.Deserialize<List<WalletDto>>(rawDto.WalletJson)?.FirstOrDefault();
                if (wallet != null)
                {
                    userDetail.TokenBalance = wallet.Balance;
                    userDetail.WalletLastUpdated = wallet.UpdatedAt;
                }
            }

            if (!string.IsNullOrEmpty(rawDto.SubscriptionsJson) && rawDto.SubscriptionsJson != "[]")
            {
                List<SubscriptionSummaryDto>? subscriptions = JsonSerializer.Deserialize<List<SubscriptionSummaryDto>>(rawDto.SubscriptionsJson);
                userDetail.Subscriptions = subscriptions;
                SubscriptionSummaryDto? activeSub = subscriptions?.FirstOrDefault(s => s.Status == "Active" && DateTime.UtcNow >= s.StartDate && DateTime.UtcNow <= s.EndDate);
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
            public TokenWallet ToDomainEntity()
            {
                return new TokenWallet(
                Id,
                UserId,
                Balance,
                IsActive,
                CreatedAt,
                UpdatedAt ?? CreatedAt // If UpdatedAt is null, use CreatedAt as a sensible default.
            );
            }
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
            await using NpgsqlConnection connection = CreateConnection();
            const string sql = @"SELECT ""Value"" FROM public.""Settings"" WHERE ""Key"" = @Key;";

            string? jsonValue = await connection.QuerySingleOrDefaultAsync<string>(
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
            await using NpgsqlConnection connection = CreateConnection();
            string jsonValue = JsonSerializer.Serialize(settings);

            const string sql = @"
        INSERT INTO public.""Settings"" (""Key"", ""Value"")
        VALUES (@Key, @Value::jsonb)
        ON CONFLICT (""Key"") DO UPDATE
        SET ""Value"" = EXCLUDED.""Value"";
    ";
            _ = await connection.ExecuteAsync(new CommandDefinition(sql, new { Key = ForceJoinSettingsKey, Value = jsonValue }, cancellationToken: cancellationToken));
            _logger.LogInformation("Force join settings have been updated in the database.");

            // CRITICAL: Invalidate the cache so the application picks up the new setting immediately.
            _ = await _cacheService.RemoveAsync(ForceJoinSettingsKey);
            _logger.LogInformation("Force join settings cache key '{CacheKey}' has been invalidated.", ForceJoinSettingsKey);
        }

        public async Task<(int UserCount, int NewsItemCount, List<(DateTime Date, int Count)> UserJoinStats)> GetDashboardStatsWithUserJoinsAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Fetching dashboard statistics with user join data.");
            try
            {
                await using NpgsqlConnection connection = CreateConnection();
                const string sql = @"
                    SELECT 
                        (SELECT COUNT(*) FROM public.""Users"") as UserCount,
                        (SELECT COUNT(*) FROM public.""NewsItems"") as NewsItemCount,
                        (SELECT jsonb_agg(jsonb_build_object('Date', DATE(""CreatedAt""), 'Count', COUNT(*)))
                         FROM (SELECT ""CreatedAt"" FROM public.""Users"" 
                               WHERE ""CreatedAt"" >= CURRENT_DATE - INTERVAL '30 days'
                               ORDER BY DATE(""CreatedAt"")) daily_users) as UserJoinStats;";

                var result = await connection.QuerySingleAsync<(int UserCount, int NewsItemCount, string? UserJoinStatsJson)>(
                    new CommandDefinition(sql, cancellationToken: cancellationToken));

                List<(DateTime Date, int Count)> userJoinStats = new();
                if (!string.IsNullOrEmpty(result.UserJoinStatsJson) && result.UserJoinStatsJson != "[]")
                {
                    var stats = JsonSerializer.Deserialize<List<dynamic>>(result.UserJoinStatsJson);
                    if (stats != null)
                    {
                        foreach (var stat in stats)
                        {
                            if (DateTime.TryParse(stat.GetProperty("Date").GetString(), out DateTime date))
                            {
                                var countStr = stat.GetProperty("Count").GetString();
                                if (int.TryParse(countStr, out int dailyCount))
                                {
                                    userJoinStats.Add((date, dailyCount));
                                }
                            }
                        }
                    }
                }

                _logger.LogInformation("Dashboard statistics with user joins retrieved successfully. Users: {UserCount}, News Items: {NewsItemCount}, Join Stats: {StatsCount} entries", 
                    result.UserCount, result.NewsItemCount, userJoinStats.Count);

                return (result.UserCount, result.NewsItemCount, userJoinStats);
            }
            catch (Exception ex)
            {
                // SECURITY: Use SecureExceptionSanitizer for logging exceptions
                var sanitizedException = SecureExceptionSanitizer.SanitizeForLogging(ex);
                _logger.LogError(sanitizedException, "Error fetching dashboard statistics with user joins.");
                throw;
            }
        }

        public async Task<AdminDashboardStatsDto> GetAdminDashboardStatsAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Fetching comprehensive admin dashboard statistics.");
            try
            {
                await using NpgsqlConnection connection = CreateConnection();
                const string sql = @"
                    SELECT 
                        (SELECT COUNT(*) FROM public.""Users"") as TotalUsers,
                        (SELECT COUNT(*) FROM public.""Users"" WHERE ""CreatedAt"" >= CURRENT_DATE) as NewUsersToday,
                        (SELECT COUNT(*) FROM public.""Users"" WHERE ""CreatedAt"" >= CURRENT_DATE - INTERVAL '7 days') as NewUsersThisWeek,
                        (SELECT COUNT(*) FROM public.""NewsItems"") as TotalNewsItems,
                        (SELECT COUNT(*) FROM public.""NewsItems"" WHERE ""CreatedAt"" >= CURRENT_DATE) as NewNewsItemsToday,
                        (SELECT COUNT(*) FROM public.""NewsItems"" WHERE ""CreatedAt"" >= CURRENT_DATE - INTERVAL '7 days') as NewNewsItemsThisWeek,
                        (SELECT COUNT(*) FROM public.""Subscriptions"" WHERE ""Status"" = 'Active') as ActiveSubscriptions,
                        (SELECT COUNT(*) FROM public.""Transactions"" WHERE ""Status"" = 'Completed' AND ""CreatedAt"" >= CURRENT_DATE - INTERVAL '30 days') as CompletedTransactionsLast30Days,
                        (SELECT COALESCE(SUM(""Amount""), 0) FROM public.""Transactions"" WHERE ""Status"" = 'Completed' AND ""CreatedAt"" >= CURRENT_DATE - INTERVAL '30 days') as TotalRevenueLast30Days;";

                // Create a custom result type that matches the SQL query
                var result = await connection.QuerySingleAsync<dynamic>(
                    new CommandDefinition(sql, cancellationToken: cancellationToken));

                // Map the result to the AdminDashboardStatsDto
                var statsDto = new AdminDashboardStatsDto
                {
                    TotalUsers = result.TotalUsers,
                    SignalsToday = result.NewNewsItemsToday, // Map to existing property
                    UserGrowthLast7Days = new List<DailyCountDto>(), // Initialize empty list
                    SignalsPerDayLast7Days = new List<DailyCountDto>() // Initialize empty list
                };

                _logger.LogInformation("Admin dashboard statistics retrieved successfully. Total Users: {TotalUsers}, Active Subscriptions: {ActiveSubscriptions}", 
                    statsDto.TotalUsers, (int)result.ActiveSubscriptions);

                return statsDto;
            }
            catch (Exception ex)
            {
                // SECURITY: Use SecureExceptionSanitizer for logging exceptions
                var sanitizedException = SecureExceptionSanitizer.SanitizeForLogging(ex);
                _logger.LogError(sanitizedException, "Error fetching admin dashboard statistics.");
                throw;
            }
        }

        public async Task<List<string>> ListLogFilesAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Listing available log files.");
            try
            {
                var logDirectory = Path.Combine(AppContext.BaseDirectory, "logs");
                if (!Directory.Exists(logDirectory))
                {
                    _logger.LogWarning("Log directory does not exist: {LogDirectory}", logDirectory);
                    return new List<string>();
                }

                var logFiles = Directory.GetFiles(logDirectory, "log-*.txt")
                    .Select(Path.GetFileName)
                    .Where(fileName => !string.IsNullOrEmpty(fileName))
                    .Cast<string>()
                    .OrderByDescending(f => f)
                    .ToList();

                // SECURITY: Sanitize file names before logging
                var sanitizedFileNames = logFiles.Select(f => SanitizeForLogging(f)).ToList();
                _logger.LogInformation("Found {Count} log files: {SanitizedFileNames}", logFiles.Count, string.Join(", ", sanitizedFileNames));

                return logFiles;
            }
            catch (Exception ex)
            {
                // SECURITY: Use SecureExceptionSanitizer for logging exceptions
                var sanitizedException = SecureExceptionSanitizer.SanitizeForLogging(ex);
                _logger.LogError(sanitizedException, "Error listing log files.");
                throw;
            }
        }

        public async Task<string?> GetLogFileContentAsync(string fileName, int? lineCount = null, CancellationToken cancellationToken = default)
        {
            // SECURITY: Validate file name before processing
            var validatedFileName = ValidateFileName(fileName);
            if (validatedFileName == null)
            {
                _logger.LogWarning("Invalid file name provided for log file content: {SanitizedFileName}", SanitizeForLogging(fileName));
                return null;
            }

            // SECURITY: Use sanitized file name for logging
            var sanitizedFileName = SanitizeForLogging(validatedFileName);
            _logger.LogInformation("Reading log file content: {SanitizedFileName}, LineCount: {LineCount}", sanitizedFileName, lineCount?.ToString() ?? "all");

            try
            {
                var logDirectory = Path.Combine(AppContext.BaseDirectory, "logs");
                var filePath = Path.Combine(logDirectory, validatedFileName);

                if (!File.Exists(filePath))
                {
                    _logger.LogWarning("Log file not found: {SanitizedFileName}", sanitizedFileName);
                    return null;
                }

                // SECURITY: Validate file size before reading
                var fileInfo = new FileInfo(filePath);
                if (fileInfo.Length > 10 * 1024 * 1024) // 10MB limit
                {
                    _logger.LogWarning("Log file too large: {SanitizedFileName}, Size: {Size} bytes", sanitizedFileName, fileInfo.Length);
                    return null;
                }

                var lines = await File.ReadAllLinesAsync(filePath, cancellationToken);
                
                if (lineCount.HasValue && lineCount.Value > 0)
                {
                    lines = lines.TakeLast(lineCount.Value).ToArray();
                }

                var content = string.Join(Environment.NewLine, lines);
                _logger.LogInformation("Successfully read log file content: {SanitizedFileName}, Lines: {LineCount}", sanitizedFileName, lines.Length);

                return content;
            }
            catch (Exception ex)
            {
                // SECURITY: Use SecureExceptionSanitizer for logging exceptions
                var sanitizedException = SecureExceptionSanitizer.SanitizeForLogging(ex);
                _logger.LogError(sanitizedException, "Error reading log file content: {SanitizedFileName}", sanitizedFileName);
                return null;
            }
        }
    }
}