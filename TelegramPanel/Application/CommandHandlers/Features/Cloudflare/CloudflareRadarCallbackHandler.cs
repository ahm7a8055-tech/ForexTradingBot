using Application.Interfaces;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Linq;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using TelegramPanel.Application.CommandHandlers.MainMenu;
using TelegramPanel.Application.Interfaces;
using TelegramPanel.Formatters;
using TelegramPanel.Infrastructure;
using TelegramPanel.Infrastructure.Helper;
using static TelegramPanel.Infrastructure.ActualTelegramMessageActions;

namespace TelegramPanel.Application.CommandHandlers.Features.Cloudflare
{
    /// <summary>
    /// Handles all callback queries for the Cloudflare Radar feature.
    /// This is the final, fully corrected version with robust display logic.
    /// </summary>
    public class CloudflareRadarCallbackHandler : ITelegramCallbackQueryHandler
    {
        public ITelegramMessageSender MessageSender => _messageSender;
        private readonly ITelegramMessageSender _messageSender;
        private readonly ILogger<CloudflareRadarCallbackHandler> _logger;
        private readonly ICloudflareRadarService _radarService;
        private readonly IActualTelegramMessageActions _directMessageSender;

        private const string CallbackPrefix = "cf_radar";
        private const string ListPageAction = "list_page";
        private const string SelectCountryAction = "select_country";
        private const int CountriesPerPage = 12;

        public CloudflareRadarCallbackHandler(
            ITelegramMessageSender messageSender,
            ILogger<CloudflareRadarCallbackHandler> logger,
            ICloudflareRadarService radarService,
            IActualTelegramMessageActions directMessageSender)
        {
            _messageSender = messageSender ?? throw new ArgumentNullException(nameof(messageSender));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _radarService = radarService ?? throw new ArgumentNullException(nameof(radarService));
            _directMessageSender = directMessageSender ?? throw new ArgumentNullException(nameof(directMessageSender));
        }

        public bool CanHandle(Update update)
        {
            return update.Type == UpdateType.CallbackQuery &&
                   update.CallbackQuery?.Data?.StartsWith(CallbackPrefix) == true;
        }


        public async Task HandleAsync(Update update, CancellationToken cancellationToken = default)
        {
            var callbackQuery = update.CallbackQuery!;
            var chatId = callbackQuery.Message!.Chat.Id;
            var messageId = callbackQuery.Message.MessageId;

            await _messageSender.AnswerCallbackQueryAsync(callbackQuery.Id, cancellationToken: cancellationToken);

            var parts = callbackQuery.Data!.Split(':');
            var action = parts[1];
            var payload = parts.Length > 2 ? parts[2] : null;

            _logger.LogInformation("CloudflareRadarCallbackHandler: Handling action '{Action}' with payload '{Payload}' for Chat {ChatId}", action, payload, chatId);

            switch (action)
            {
                case ListPageAction when int.TryParse(payload, out int page):
                    await ShowCountrySelectionMenuAsync(chatId, messageId, page, cancellationToken);
                    break;
                case SelectCountryAction when !string.IsNullOrEmpty(payload):
                    await ShowCountryReportAsync(chatId, messageId, payload, cancellationToken);
                    break;
                default:
                    _logger.LogWarning("CloudflareRadarCallbackHandler: Unhandled action '{Action}' for Chat {ChatId}.", action, chatId);
                    await _directMessageSender.EditMessageTextDirectAsync(
                        chatId, messageId, "Sorry, this action is not recognized.", ParseMode.Markdown, null, cancellationToken);
                    break;
            }
        }

        public async Task ShowCountrySelectionMenuAsync(long chatId, int messageId, int page, CancellationToken cancellationToken)
        {
            var text = "☁️ *Cloudflare Radar*\n\nSelect a country to view its internet health report.";
            var totalCountries = CountryHelper.AllCountries.Count;
            var totalPages = (int)Math.Ceiling((double)totalCountries / CountriesPerPage);
            page = Math.Clamp(page, 1, totalPages);

            var countriesToShow = CountryHelper.AllCountries.Skip((page - 1) * CountriesPerPage).Take(CountriesPerPage).ToList();

            var buttonRows = new List<List<InlineKeyboardButton>>();
            for (int i = 0; i < countriesToShow.Count; i += 3)
            {
                buttonRows.Add(countriesToShow.Skip(i).Take(3).Select(c => InlineKeyboardButton.WithCallbackData(c.Name, $"{CallbackPrefix}:{SelectCountryAction}:{c.Code}")).ToList());
            }

            var navRow = new List<InlineKeyboardButton>();
            if (page > 1) navRow.Add(InlineKeyboardButton.WithCallbackData("⬅️ Prev", $"{CallbackPrefix}:{ListPageAction}:{page - 1}"));
            navRow.Add(InlineKeyboardButton.WithCallbackData($"Page {page}/{totalPages}", "noop"));
            if (page < totalPages) navRow.Add(InlineKeyboardButton.WithCallbackData("Next ➡️", $"{CallbackPrefix}:{ListPageAction}:{page + 1}"));
            buttonRows.Add(navRow);
            buttonRows.Add([InlineKeyboardButton.WithCallbackData("🏠 Main Menu", MenuCommandHandler.BackToMainMenuGeneral)]);

            var keyboard = new InlineKeyboardMarkup(buttonRows);
            await _directMessageSender.EditMessageTextDirectAsync(chatId, messageId, text, ParseMode.Markdown, keyboard, cancellationToken);
            _logger.LogInformation("Sent country selection menu (Page {Page}) to Chat {ChatId}", page, chatId);
        }

        private async Task ShowCountryReportAsync(long chatId, int messageId, string countryCode, CancellationToken cancellationToken)
        {
            var countryName = CountryHelper.AllCountries.FirstOrDefault(c => c.Code == countryCode).Name ?? countryCode;
            var safeCountryName = TelegramMessageFormatter.EscapeMarkdownV2(countryName);

            try
            {
                var reportResult = await AnimateWhileExecutingAsync(chatId, messageId,
                    $"☁️ Analyzing internet health for *{safeCountryName}*",
                    ct => _radarService.GetCountryReportAsync(countryCode, ct),
                    cancellationToken);

                if (reportResult.Succeeded && reportResult.Data != null)
                {
                    var data = reportResult.Data;
                    var caption = FormatCountryReportMessage(data); // REWRITTEN LOGIC IS IN THIS METHOD

                    var keyboard = new InlineKeyboardMarkup(new List<List<InlineKeyboardButton>>
                    {
                        new() { InlineKeyboardButton.WithUrl("View Full Report on Cloudflare Radar ↗️", data.RadarUrl) },
                        new() { InlineKeyboardButton.WithCallbackData("⬅️ Back to Country List", $"{CallbackPrefix}:{ListPageAction}:1") }
                    });

                    await _directMessageSender.DeleteMessageAsync(chatId, messageId, cancellationToken);
                    await _messageSender.SendTextMessageAsync(chatId: chatId, text: caption, parseMode: ParseMode.MarkdownV2, replyMarkup: keyboard, cancellationToken: cancellationToken);
                    _logger.LogInformation("Sent Cloudflare Radar report for {CountryCode} to Chat {ChatId}.", countryCode, chatId);
                }
                else
                {
                    var errorText = $"❌ Could not retrieve report for *{safeCountryName}*.\n`{TelegramMessageFormatter.EscapeMarkdownV2(reportResult.Errors.FirstOrDefault() ?? "Unknown error.")}`";
                    var keyboard = new InlineKeyboardMarkup(InlineKeyboardButton.WithCallbackData("⬅️ Back to Countries", $"{CallbackPrefix}:{ListPageAction}:1"));
                    await _directMessageSender.EditMessageTextDirectAsync(chatId, messageId, errorText, ParseMode.MarkdownV2, keyboard, cancellationToken);
                    _logger.LogWarning("Failed to retrieve report for {CountryCode} for Chat {ChatId}. Error: {Error}", countryCode, chatId, reportResult.Errors.FirstOrDefault());
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while showing Cloudflare report for {CountryCode} to Chat {ChatId}.", countryCode, chatId);
                var keyboard = new InlineKeyboardMarkup(InlineKeyboardButton.WithCallbackData("⬅️ Try Again", $"{CallbackPrefix}:{SelectCountryAction}:{countryCode}"));
                await _directMessageSender.EditMessageTextDirectAsync(chatId, messageId, "An unexpected error occurred. Please try again.", ParseMode.MarkdownV2, replyMarkup: keyboard, cancellationToken: CancellationToken.None);
            }
        }

        private string GenerateProgressBar(double percentage, int size = 10, string filled = "🟩", string empty = "⬜️")
        {
            if (double.IsNaN(percentage) || percentage < 0) percentage = 0;
            if (percentage > 100) percentage = 100;
            int filledBlocks = (int)Math.Round(percentage / 100.0 * size);
            return string.Concat(Enumerable.Repeat(filled, filledBlocks)) + string.Concat(Enumerable.Repeat(empty, size - filledBlocks));
        }

        // --- START OF REWRITTEN METHOD ---
        // This version has major formatting upgrades, more progress bars, and new sections.
        private string FormatCountryReportMessage(CloudflareCountryReportDto data)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"☁️ *Internet Report: {TelegramMessageFormatter.EscapeMarkdownV2(data.CountryName)}*");
            sb.AppendLine("`-----------------------------------`");

            // --- Section: Stability (Outages & Anomalies) ---
            if (data.LatestOutage != null)
            {
                var outage = data.LatestOutage;
                sb.AppendLine($"*🔴 Confirmed Outage*");
                sb.AppendLine($"  - Cause: `{TelegramMessageFormatter.EscapeMarkdownV2(outage.Cause)}`");
                sb.AppendLine($"  - Started: `{outage.StartDate:MMM dd, HH:mm} UTC`");
                sb.AppendLine();
            }
            else
            {
                sb.AppendLine("*✅ Stability:* `No outages detected`");
                sb.AppendLine();
            }

            // --- Section: Bot vs Human Traffic ---
            if (data.BotVsHumanTraffic != null)
            {
                var bots = data.BotVsHumanTraffic;
                sb.AppendLine($"*👥 Traffic Source (24h)*");
                sb.AppendLine($"  - 👨‍💻 Humans: `{bots.Human:F1}%`");
                sb.AppendLine($"    `{GenerateProgressBar(bots.Human)}`");
                sb.AppendLine($"  - 🤖 Bots: `{bots.Bot:F1}%`");
                sb.AppendLine();
            }

            // --- Section: Layer 7 Attacks & Mitigation ---
            if (data.Layer7Attacks != null && data.Layer7Attacks.PercentageOfTotal > 0)
            {
                var attack = data.Layer7Attacks;
                sb.AppendLine($"*🛡️ L7 DDoS Attacks (7d)*");
                // BUG FIX: Only show origin if it's known
                if (attack.TopSourceCountry != "Unknown")
                {
                    sb.AppendLine($"   - Top Origin: `{TelegramMessageFormatter.EscapeMarkdownV2(attack.TopSourceCountry)}`");
                }
                sb.AppendLine($"   - Share of Total: `{attack.PercentageOfTotal:F1}%`");

                if (data.AttackMitigation != null)
                {
                    var m = data.AttackMitigation;
                    var totalMitigated = m.Waf + m.RateLimiting + m.BotManagement;
                    if (totalMitigated > 0)
                    {
                        sb.AppendLine($"\n  *🛠️ Top Mitigation Methods*");
                        if (m.Waf > 0) sb.AppendLine($"   - WAF: `{m.Waf:F1}%`");
                        if (m.RateLimiting > 0) sb.AppendLine($"   - Rate Limiting: `{m.RateLimiting:F1}%`");
                        if (m.BotManagement > 0) sb.AppendLine($"   - Bot Management: `{m.BotManagement:F1}%`");
                    }
                }
                sb.AppendLine();
            }

            // --- Section: HTTP, TLS & IP Version ---
            if (data.HttpProtocolDistribution != null)
            {
                var http = data.HttpProtocolDistribution;
                sb.AppendLine($"*🚀 Protocol Adoption*");
                sb.AppendLine($"  - HTTP/3: `{http.Http3:F1}%` / HTTP/2: `{http.Http2:F1}%`");
                sb.AppendLine($"    `{GenerateProgressBar(http.Http3 + http.Http2)}`");

                if (data.TlsVersionDistribution != null)
                {
                    var tls = data.TlsVersionDistribution;
                    sb.AppendLine($"\n  *🔒 Modern TLS Adoption (1.3)*");
                    sb.AppendLine($"   - TLS 1.3: `{tls.Tls13:F1}%` / TLS 1.2: `{tls.Tls12:F1}%`");
                    sb.AppendLine($"    `{GenerateProgressBar(tls.Tls13)}`");
                }

                if (data.IpVersionDistribution != null)
                {
                    var ip = data.IpVersionDistribution;
                    sb.AppendLine($"\n  *🌐 IPv6 Adoption*");
                    sb.AppendLine($"   - IPv6: `{ip.Ipv6:F1}%` / IPv4: `{ip.Ipv4:F1}%`");
                    sb.AppendLine($"    `{GenerateProgressBar(ip.Ipv6)}`");
                }
                sb.AppendLine();
            }

            // --- Section: Device Type Distribution ---
            if (data.DeviceTypeDistribution != null)
            {
                var devices = data.DeviceTypeDistribution;
                sb.AppendLine($"*💻 Device Types (Desktop)*");
                sb.AppendLine($"  - Desktop: `{devices.Desktop:F1}%` / Mobile: `{devices.Mobile:F1}%`");
                sb.AppendLine($"    `{GenerateProgressBar(devices.Desktop)}`");
                sb.AppendLine();
            }

            sb.AppendLine("`-----------------------------------`");
            if (!string.IsNullOrWhiteSpace(data.ReportTimestamp) && DateTime.TryParse(data.ReportTimestamp, out var reportTime))
            {
                sb.AppendLine($"_Data from Cloudflare Radar as of {reportTime:MMM dd, HH:mm} UTC._");
            }
            else
            {
                sb.AppendLine($"_Data from Cloudflare Radar as of {DateTime.UtcNow:MMM dd, HH:mm} UTC._");
            }

            return sb.ToString();
        }
        // --- END OF REWRITTEN METHOD ---

        private async Task<TResult> AnimateWhileExecutingAsync<TResult>(long chatId, int messageId, string baseText, Func<CancellationToken, Task<TResult>> operationToExecute, CancellationToken cancellationToken)
        {
            var animationFrames = new[] { "·", "··", "···" };
            var frameIndex = 0;
            var operationTask = operationToExecute(cancellationToken);

            var animationTask = Task.Run(async () =>
            {
                while (!operationTask.IsCompleted && !cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        var text = $"{baseText} {animationFrames[frameIndex++ % animationFrames.Length]}";
                        await _directMessageSender.EditMessageTextDirectAsync(chatId, messageId, text, ParseMode.MarkdownV2, null, cancellationToken);
                    }
                    catch (ApiRequestException apiEx) when (apiEx.Message.Contains("not modified")) { /* Ignore */ }
                    catch (OperationCanceledException) { break; }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Animation loop encountered an error for Chat {ChatId}, Message {MessageId}. Animation will stop.", chatId, messageId);
                        break;
                    }
                    await Task.Delay(800, CancellationToken.None);
                }
            }, CancellationToken.None);

            try { return await operationTask; }
            finally
            {
                try { await animationTask; } catch { /* Suppress final animation error */ }
            }
        }
    }
}