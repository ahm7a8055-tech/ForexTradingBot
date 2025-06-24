// --- START OF FILE: Infrastructure/Services/AdminService.cs ---

using Application.DTOs.Admin;
using Application.Interfaces;
using Dapper;
using Domain.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.IO.Compression;
using System.Text;

namespace Infrastructure.Services.Admin
{
    public class AdminService : IAdminService
    {
        private readonly string _connectionString;
        private readonly ILogger<AdminService> _logger;

        public AdminService(IConfiguration configuration, ILogger<AdminService> logger)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")!;
            _logger = logger;
        }

        public async Task<(int UserCount, int NewsItemCount)> GetDashboardStatsAsync(CancellationToken cancellationToken = default)
        {
            await using var connection = new SqlConnection(_connectionString);
            var sql = "SELECT COUNT(1) FROM dbo.Users; SELECT COUNT(1) FROM dbo.NewsItems;";
            using var multi = await connection.QueryMultipleAsync(new CommandDefinition(sql, cancellationToken: cancellationToken));
            return (await multi.ReadSingleAsync<int>(), await multi.ReadSingleAsync<int>());
        }

        public async Task<List<long>> GetAllActiveUserChatIdsAsync(CancellationToken cancellationToken = default)
        {
            await using var connection = new SqlConnection(_connectionString);
            var sql = "SELECT TelegramId FROM dbo.Users WHERE TelegramId IS NOT NULL AND TelegramId <> '';";
            var idsAsString = await connection.QueryAsync<string>(new CommandDefinition(sql, cancellationToken: cancellationToken));

            var userChatIds = new List<long>();
            foreach (var idStr in idsAsString)
            {
                if (long.TryParse(idStr, out var id))
                {
                    userChatIds.Add(id);
                }
                else
                {
                    _logger.LogWarning("Could not parse TelegramId '{IdString}' to long.", idStr);
                }
            }
            return userChatIds;
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
            _logger.LogWarning("Admin is executing a raw SQL query: {Query}", sqlQuery);
            await using var connection = new SqlConnection(_connectionString);
            var response = new StringBuilder();

            try
            {
                var command = new CommandDefinition(sqlQuery, commandTimeout: 60, cancellationToken: cancellationToken);

                // Use QueryMultiple for flexibility, as the query could be anything.
                using var multi = await connection.QueryMultipleAsync(command);

                int resultSetIndex = 1;
                while (!multi.IsConsumed)
                {
                    var grid = await multi.ReadAsync();
                    var data = grid.ToList();

                    if (!data.Any())
                    {
                        _ = response.AppendLine($"-- Result Set {resultSetIndex} (No Rows) --\n");
                        resultSetIndex++;
                        continue;
                    }

                    _ = response.AppendLine($"-- Result Set {resultSetIndex} ({data.Count} Rows) --");
                    // Get headers from the first row (which is an IDictionary<string, object>)
                    var headers = ((IDictionary<string, object>)data.First()).Keys;
                    _ = response.AppendLine("`" + string.Join(" | ", headers) + "`");

                    foreach (var row in data)
                    {
                        var rowDict = (IDictionary<string, object>)row;
                        var values = rowDict.Values.Select(v => v?.ToString() ?? "NULL");
                        _ = response.AppendLine("`" + string.Join(" | ", values) + "`");
                    }
                    _ = response.AppendLine();
                    resultSetIndex++;
                }
                return response.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing raw SQL query.");
                return $"❌ **SQL Execution Error:**\n`{ex.Message}`";
            }
        }



        // ✅ This is the single, correct implementation for the detailed user lookup.
        public async Task<AdminUserDetailDto?> GetUserDetailByTelegramIdAsync(long telegramId, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Fetching detailed profile for Telegram ID: {TelegramId}", telegramId);
            await using var connection = new SqlConnection(_connectionString);

            var sql = @"
                SELECT * FROM dbo.Users WHERE TelegramId = @TelegramIdStr;
                SELECT Balance, UpdatedAt AS WalletLastUpdated FROM dbo.TokenWallets WHERE UserId = (SELECT Id FROM dbo.Users WHERE TelegramId = @TelegramIdStr);
                SELECT Id AS SubscriptionId, StartDate, EndDate, Status FROM dbo.Subscriptions WHERE UserId = (SELECT Id FROM dbo.Users WHERE TelegramId = @TelegramIdStr) ORDER BY StartDate DESC;
                SELECT TOP 10 Id AS TransactionId, Amount, Type, Status, Timestamp FROM dbo.Transactions WHERE UserId = (SELECT Id FROM dbo.Users WHERE TelegramId = @TelegramIdStr) ORDER BY Timestamp DESC;
            ";

            using var multi = await connection.QueryMultipleAsync(sql, new { TelegramIdStr = telegramId.ToString() });

            var user = await multi.ReadSingleOrDefaultAsync<User>();
            if (user == null)
            {
                return null;
            }

            var userDetail = new AdminUserDetailDto
            {
                UserId = user.Id,
                Username = user.Username,
                TelegramId = long.Parse(user.TelegramId)
            };
            // ... etc.

            var walletInfo = await multi.ReadSingleOrDefaultAsync();
            if (walletInfo != null)
            {
                userDetail.TokenBalance = walletInfo.Balance;
                userDetail.WalletLastUpdated = walletInfo.WalletLastUpdated;
            }

            var subscriptions = (await multi.ReadAsync<SubscriptionSummaryDto>()).ToList();
            if (subscriptions.Any())
            {
                userDetail.Subscriptions = subscriptions;
                var activeSub = subscriptions.FirstOrDefault(s => s.Status == "Active" && DateTime.UtcNow >= s.StartDate && DateTime.UtcNow <= s.EndDate);
                if (activeSub != null)
                {
                    userDetail.ActiveSubscription = new ActiveSubscriptionDto { EndDate = activeSub.EndDate };
                    // ...
                }
            }

            var transactions = (await multi.ReadAsync<TransactionSummaryDto>()).ToList();
            if (transactions.Any())
            {
                userDetail.RecentTransactions = transactions;
                // ... calculate total spent etc. ...
            }

            return userDetail;
        }
    }
}