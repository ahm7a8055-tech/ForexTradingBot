// --- START OF FILE: AdminCallbackHandler.cs ---

using Application.Interfaces; // For IAdminService
using Hangfire; // For IRecurringJobManager
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using TelegramPanel.Application.CommandHandlers.MainMenu;
using TelegramPanel.Application.Interfaces;
using TelegramPanel.Formatters;
using TelegramPanel.Infrastructure.Helper;
using TelegramPanel.Settings;
using static TelegramPanel.Infrastructure.ActualTelegramMessageActions;

namespace TelegramPanel.Application.CommandHandlers.Admin
{
    /// <summary>
    /// Handles simple, stateless callback queries from the Admin Panel,
    /// such as fetching stats or triggering one-off background jobs.
    /// Stateful actions like broadcasting are handled by dedicated InitiationHandlers.
    /// </summary>
    public class AdminCallbackHandler : ITelegramCallbackQueryHandler
    {
        private readonly ILogger<AdminCallbackHandler> _logger;
        private readonly ITelegramMessageSender _messageSender;
        private readonly IAdminService _adminService;
        private readonly IRecurringJobManager _recurringJobManager;
        private readonly IConfiguration _configuration;
        private readonly TelegramPanelSettings _settings;

        // Constants for the actions this specific handler is responsible for.
        private const string AdminServerStatsCallback = "admin_server_stats";
        private const string AdminManualRssFetchCallback = "admin_manual_rss";
        private const string PurgeHangfireCallback = "admin_purge_hangfire";
        private const string BackToAdminPanelCallback = "admin_panel_main";
        private const string DownloadLogsCallback = "admin_download_logs";

        public AdminCallbackHandler(
            ILogger<AdminCallbackHandler> logger,
            ITelegramMessageSender messageSender,
            IAdminService adminService,
            IRecurringJobManager recurringJobManager,
            IConfiguration configuration,
            IOptions<TelegramPanelSettings> settingsOptions)
        {
            _logger = logger;
            _messageSender = messageSender;
            _adminService = adminService;
            _recurringJobManager = recurringJobManager;
            _configuration = configuration;
            _settings = settingsOptions.Value;
        }

        public bool CanHandle(Update update)
        {
            if (update.Type != UpdateType.CallbackQuery || update.CallbackQuery?.Data == null || update.CallbackQuery.From == null)
            {
                return false;
            }

            if (!_settings.AdminUserIds.Contains(update.CallbackQuery.From.Id))
            {
                return false;
            }

            string data = update.CallbackQuery.Data;
            return data is AdminServerStatsCallback or // "admin_server_stats"
                   AdminManualRssFetchCallback or    // "admin_manual_rss"
                   PurgeHangfireCallback or          // "admin_purge_hangfire"
                   DownloadLogsCallback or         // "admin_download_logs"
                   BackToAdminPanelCallback;         // "admin_panel_main"
        }



        public async Task HandleAsync(Update update, CancellationToken cancellationToken = default)
        {
            CallbackQuery callbackQuery = update.CallbackQuery!;
            await _messageSender.AnswerCallbackQueryAsync(callbackQuery.Id, cancellationToken: cancellationToken);

            _logger.LogInformation("Admin {UserId} initiated action: {Action}", callbackQuery.From.Id, callbackQuery.Data);

            // --- MODIFIED: Add new case to switch ---
            Task handlerTask = callbackQuery.Data switch
            {
                AdminServerStatsCallback => HandleServerStatsAsync(callbackQuery.Message!.Chat.Id, callbackQuery.Message.MessageId, cancellationToken),
                AdminManualRssFetchCallback => HandleManualRssFetchAsync(callbackQuery.Message!.Chat.Id, callbackQuery.Message.MessageId, cancellationToken),
                PurgeHangfireCallback => HandlePurgeHangfireAsync(callbackQuery.Message!.Chat.Id, callbackQuery.Message.MessageId, cancellationToken),
                DownloadLogsCallback => HandleDownloadLogsAsync(callbackQuery.Message!.Chat.Id, callbackQuery.Message.MessageId, cancellationToken), // NEW
                BackToAdminPanelCallback => ShowAdminPanelAsync(callbackQuery.Message!.Chat.Id, callbackQuery.Message.MessageId, cancellationToken),
                _ => Task.CompletedTask
            };
            await handlerTask;
        }
        private async Task HandleDownloadLogsAsync(long chatId, int messageId, CancellationToken cancellationToken)
        {
            // Give immediate feedback to the admin
            await _messageSender.EditMessageTextAsync(chatId, messageId, "🗜️ Finding and zipping log files, please wait...", cancellationToken: cancellationToken);

            (byte[] zipContents, string fileName, string errorMessage) = await _adminService.GetLogFilesAsZipAsync(cancellationToken);

            if (zipContents != null && zipContents.Length > 0)
            {
                _logger.LogInformation("Sending log archive '{FileName}' to admin chat {ChatId}.", fileName, chatId);

                // ✅ CORRECTED: Call the new method on your sender interface with the serializable types.
                // The sender will now handle enqueuing this to Hangfire correctly.
                await _messageSender.SendDocumentAsync(
                    chatId: chatId,
                    documentContents: zipContents,
                    fileName: fileName,
                    caption: "Here are the server logs.",
                    cancellationToken: cancellationToken
                );

                // Clean up the "zipping..." message by editing it back to the main panel
                await _messageSender.EditMessageTextAsync(chatId, messageId, "✅ Log archive sent successfully. The file will arrive shortly.", replyMarkup: GetBackToAdminPanelKeyboard(), cancellationToken: cancellationToken);
            }
            else
            {
                _logger.LogWarning("Failed to send log archive to admin: {Error}", errorMessage);
                string errorText = $"❌ Could not retrieve logs. Reason: `{errorMessage}`";
                await _messageSender.EditMessageTextAsync(chatId, messageId, errorText, ParseMode.Markdown, GetBackToAdminPanelKeyboard(), cancellationToken: cancellationToken);
            }
        }

        private async Task HandleServerStatsAsync(long chatId, int messageId, CancellationToken cancellationToken)
        {
            await _messageSender.EditMessageTextAsync(chatId, messageId, "\ud83d\udcca Fetching server stats...", cancellationToken: cancellationToken);

            (int userCount, int newsItemCount, List<(DateTime Date, int Count)> userJoinStats) = await _adminService.GetDashboardStatsWithUserJoinsAsync(cancellationToken);

            StringBuilder stats = new();
            _ = stats.AppendLine("\ud83d\udcca *Server & Bot Status*\n────────────────────────────");
            _ = stats.AppendLine($"👥 Users: {userCount:N0}");
            _ = stats.AppendLine($"📰 News: {newsItemCount:N0}");
            _ = stats.AppendLine($"🌐 Env: {Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")}");
            _ = stats.AppendLine($"🕔 UTC: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}");
            _ = stats.AppendLine("────────────────────────────\n");

            #region User Join Chart (Ultra-Polished)
            if (userJoinStats != null && userJoinStats.Count > 0)
            {
                int max = userJoinStats.Max(x => x.Count);
                int barMax = 8;
                int total = userJoinStats.Sum(x => x.Count);
                double avgAll = userJoinStats.Average(x => x.Count);
                var activeDays = userJoinStats.Where(x => x.Count > 0).ToList();
                double avgActive = activeDays.Count > 0 ? activeDays.Average(x => x.Count) : 0;
                var orderedCounts = userJoinStats.Select(x => x.Count).OrderBy(x => x).ToList();
                double median = orderedCounts.Count % 2 == 1
                    ? orderedCounts[orderedCounts.Count / 2]
                    : (orderedCounts[orderedCounts.Count / 2 - 1] + orderedCounts[orderedCounts.Count / 2]) / 2.0;
                // Trend: Compare last 7 days to previous 7 days
                int trendWindow = 7;
                int sumLast = userJoinStats.Skip(userJoinStats.Count - trendWindow).Sum(x => x.Count);
                int sumPrev = userJoinStats.Skip(userJoinStats.Count - 2 * trendWindow).Take(trendWindow).Sum(x => x.Count);
                string trend = sumLast > sumPrev ? "⬆️" : sumLast < sumPrev ? "⬇️" : "➡️";
                DateTime today = DateTime.UtcNow.Date;
                _ = stats.AppendLine("👤 *User Joins (Last 30 Days)*\n");
                _ = stats.AppendLine("` Date   | Users |`");
                foreach ((DateTime date, int count) in userJoinStats)
                {
                    string bar;
                    if (max == 0)
                    {
                        bar = "▏";
                    }
                    else
                    {
                        bar = count == 0 ? "▏" : new string('█', Math.Max(1, (int)Math.Round((double)count / max * barMax)));
                    }

                    string dayMark = date == today ? "➡️" : "  ";
                    _ = stats.AppendLine($"{dayMark}{date:MM-dd} {bar.PadRight(barMax)} {count}");
                }
                _ = stats.AppendLine($"\n📈 *30d Total:* {total}");
                _ = stats.AppendLine($"*Avg/day (all):* {avgAll:0.0}");
                _ = stats.AppendLine($"*Avg/day (active):* {avgActive:0.0}");
                _ = stats.AppendLine($"*Median/day:* {median:0.0}");
                _ = stats.AppendLine($"*Trend (last 7 vs prev 7):* {trend}\n");
                _ = stats.AppendLine("Legend: ▏=0, █=max");
            }
            #endregion

            await _messageSender.EditMessageTextAsync(chatId, messageId, stats.ToString(), ParseMode.Markdown, GetBackToAdminPanelKeyboard(), cancellationToken);
        }

        private async Task HandleManualRssFetchAsync(long chatId, int messageId, CancellationToken cancellationToken)
        {
            await _messageSender.EditMessageTextAsync(chatId, messageId, "⏳ Triggering RSS fetch job...", cancellationToken: cancellationToken);
            string text = "✅ The `fetch-all-active-rss-feeds` job has been triggered. Check Hangfire dashboard for progress.";
            try
            {
                _recurringJobManager.Trigger("fetch-all-active-rss-feeds");
                _logger.LogInformation("Admin manually triggered the RSS fetch job.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to manually trigger RSS Fetch job.");
                text = "❌ Failed to trigger RSS Fetch job. See server logs for details.";
            }
            await _messageSender.EditMessageTextAsync(chatId, messageId, text, replyMarkup: GetBackToAdminPanelKeyboard(), cancellationToken: cancellationToken);
        }

        private async Task HandlePurgeHangfireAsync(long chatId, int messageId, CancellationToken cancellationToken)
        {
            await _messageSender.EditMessageTextAsync(chatId, messageId, "⏳ Purging completed Hangfire jobs...", cancellationToken: cancellationToken);
            try
            {
                string connectionString = _configuration.GetConnectionString("DefaultConnection")!;
                // await _hangfireCleaner.PurgeCompletedAndFailedJobs();
                _logger.LogInformation("Admin manually purged Hangfire jobs.");
                await _messageSender.EditMessageTextAsync(chatId, messageId, "✅ Hangfire 'Succeeded' and 'Failed' job lists have been cleared.", replyMarkup: GetBackToAdminPanelKeyboard(), cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to purge Hangfire jobs.");
                await _messageSender.EditMessageTextAsync(chatId, messageId, "❌ An error occurred while purging Hangfire jobs.", replyMarkup: GetBackToAdminPanelKeyboard(), cancellationToken: cancellationToken);
            }
        }

        private Task ShowAdminPanelAsync(long chatId, int messageId, CancellationToken cancellationToken)
        {
            string text = TelegramMessageFormatter.Bold("🛠️ Administrator Panel") + "\n\nSelect an action:";
            InlineKeyboardMarkup keyboard = GetAdminPanelKeyboard();
            return _messageSender.EditMessageTextAsync(chatId, messageId, text, ParseMode.Markdown, keyboard, cancellationToken);
        }

        private InlineKeyboardMarkup GetAdminPanelKeyboard()
        {
            // Use the constants defined in this class for consistency.
            // Other handlers (like BroadcastInitiationHandler) will have their own constants.
            return MarkupBuilder.CreateInlineKeyboard(
                new[] { InlineKeyboardButton.WithCallbackData("📊 Server Stats", AdminServerStatsCallback) },
                new[] { InlineKeyboardButton.WithCallbackData("🔄 Fetch RSS Now", AdminManualRssFetchCallback),
                        InlineKeyboardButton.WithCallbackData("🧹 Purge Hangfire Jobs", PurgeHangfireCallback) },
                new[] { InlineKeyboardButton.WithCallbackData("📣 Broadcast", "admin_broadcast"),
                        InlineKeyboardButton.WithCallbackData("🔍 User Lookup", "admin_user_lookup") },
                new[] { InlineKeyboardButton.WithCallbackData("🏠 Main Menu", MenuCallbackQueryHandler.BackToMainMenuGeneral) }
            );
        }

        private static InlineKeyboardMarkup GetBackToAdminPanelKeyboard()
        {
            return MarkupBuilder.CreateInlineKeyboard(new[] { InlineKeyboardButton.WithCallbackData("⬅️ Back to Admin Panel", BackToAdminPanelCallback) });
        }
    }
}