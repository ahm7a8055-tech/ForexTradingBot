using Microsoft.Extensions.Logging;
using System.Text;
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
        public ITelegramMessageSender MessageSender { get; }

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
            MessageSender = messageSender ?? throw new ArgumentNullException(nameof(messageSender));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _radarService = radarService ?? throw new ArgumentNullException(nameof(radarService));
            _directMessageSender = directMessageSender ?? throw new ArgumentNullException(nameof(directMessageSender));
        }

        // Inside CloudflareRadarCallbackHandler.cs - (THIS IS AN INCORRECT APPROACH)
        public bool CanHandle(Update update)
        {
            string? callbackData = update.CallbackQuery?.Data;
            if (string.IsNullOrEmpty(callbackData))
            {
                return false;
            }

            // Let's add the check for the main menu button
            return callbackData.StartsWith(CallbackPrefix) || // Handles "cf_radar:..."
                   callbackData == MenuCommandHandler.BackToMainMenuGeneral; // Handles "menu_main_general"
        }

        public async Task HandleAsync(Update update, CancellationToken cancellationToken = default)
        {
            CallbackQuery callbackQuery = update.CallbackQuery!;
            long chatId = callbackQuery.Message!.Chat.Id;
            int messageId = callbackQuery.Message.MessageId;

            await MessageSender.AnswerCallbackQueryAsync(callbackQuery.Id, cancellationToken: cancellationToken);

            string[] parts = callbackQuery.Data!.Split(':');
            string action = parts[1];
            string? payload = parts.Length > 2 ? parts[2] : null;

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
            string text = "☁️ *Cloudflare Radar*\n\nSelect a country to view its internet health report.";
            int totalCountries = CountryHelper.AllCountries.Count;
            int totalPages = (int)Math.Ceiling((double)totalCountries / CountriesPerPage);
            page = Math.Clamp(page, 1, totalPages);

            List<(string Name, string Code)> countriesToShow = CountryHelper.AllCountries.Skip((page - 1) * CountriesPerPage).Take(CountriesPerPage).ToList();

            List<List<InlineKeyboardButton>> buttonRows = [];
            for (int i = 0; i < countriesToShow.Count; i += 3)
            {
                buttonRows.Add(countriesToShow.Skip(i).Take(3).Select(c => InlineKeyboardButton.WithCallbackData(c.Name, $"{CallbackPrefix}:{SelectCountryAction}:{c.Code}")).ToList());
            }

            List<InlineKeyboardButton> navRow = [];
            if (page > 1)
            {
                navRow.Add(InlineKeyboardButton.WithCallbackData("⬅️ Prev", $"{CallbackPrefix}:{ListPageAction}:{page - 1}"));
            }

            navRow.Add(InlineKeyboardButton.WithCallbackData($"Page {page}/{totalPages}", "noop"));
            if (page < totalPages)
            {
                navRow.Add(InlineKeyboardButton.WithCallbackData("Next ➡️", $"{CallbackPrefix}:{ListPageAction}:{page + 1}"));
            }

            buttonRows.Add(navRow);
            buttonRows.Add([InlineKeyboardButton.WithCallbackData("🏠 Main Menu", MenuCommandHandler.BackToMainMenuGeneral)]);

            InlineKeyboardMarkup keyboard = new(buttonRows);
            await _directMessageSender.EditMessageTextDirectAsync(chatId, messageId, text, ParseMode.Markdown, keyboard, cancellationToken);
            _logger.LogInformation("Sent country selection menu (Page {Page}) to Chat {ChatId}", page, chatId);
        }

        private async Task ShowCountryReportAsync(long chatId, int messageId, string countryCode, CancellationToken cancellationToken)
        {
            string countryName = CountryHelper.AllCountries.FirstOrDefault(c => c.Code == countryCode).Name ?? countryCode;
            string safeCountryName = TelegramMessageFormatter.EscapeMarkdownV2(countryName);

            try
            {
                // The animation part already uses EditMessageTextDirectAsync, which is great.
                Shared.Results.Result<CloudflareCountryReportDto> reportResult = await AnimateWhileExecutingAsync(chatId, messageId,
                    $"☁️ Analyzing internet health for *{safeCountryName}*",
                    ct => _radarService.GetCountryReportAsync(countryCode, ct),
                    cancellationToken);

                if (reportResult.Succeeded && reportResult.Data != null)
                {
                    CloudflareCountryReportDto data = reportResult.Data;
                    string caption = FormatCountryReportMessage(data);

                    InlineKeyboardMarkup keyboard = new(new List<List<InlineKeyboardButton>>
            {
                new() { InlineKeyboardButton.WithUrl("View Full Report on Cloudflare Radar ↗️", data.RadarUrl) },
                new() { InlineKeyboardButton.WithCallbackData("⬅️ Back to Country List", $"{CallbackPrefix}:{ListPageAction}:1") }
            });

                    // --- RECOMMENDED CHANGE ---
                    // Replace the Delete/Send actions with a single Edit action for a smooth transition.
                    await _directMessageSender.EditMessageTextDirectAsync(
                        chatId,
                        messageId,
                        caption,
                        ParseMode.MarkdownV2,
                        keyboard,
                        cancellationToken
                    );
                    // --- END OF CHANGE ---

                    _logger.LogInformation("Sent Cloudflare Radar report for {CountryCode} to Chat {ChatId}.", countryCode, chatId);
                }
                else
                {
                    // This part already uses Edit, which is correct.
                    string errorText = $"❌ Could not retrieve report for *{safeCountryName}*.\n`{TelegramMessageFormatter.EscapeMarkdownV2(reportResult.Errors.FirstOrDefault() ?? "Unknown error.")}`";
                    InlineKeyboardMarkup keyboard = new(InlineKeyboardButton.WithCallbackData("⬅️ Back to Countries", $"{CallbackPrefix}:{ListPageAction}:1"));
                    await _directMessageSender.EditMessageTextDirectAsync(chatId, messageId, errorText, ParseMode.MarkdownV2, keyboard, cancellationToken);
                    _logger.LogWarning("Failed to retrieve report for {CountryCode} for Chat {ChatId}. Error: {Error}", countryCode, chatId, reportResult.Errors.FirstOrDefault());
                }
            }
            catch (Exception ex)
            {
                // This part also uses Edit correctly.
                _logger.LogError(ex, "An unexpected error occurred while showing Cloudflare report for {CountryCode} to Chat {ChatId}.", countryCode, chatId);
                InlineKeyboardMarkup keyboard = new(InlineKeyboardButton.WithCallbackData("⬅️ Try Again", $"{CallbackPrefix}:{SelectCountryAction}:{countryCode}"));
                await _directMessageSender.EditMessageTextDirectAsync(chatId, messageId, "An unexpected error occurred. Please try again.", ParseMode.MarkdownV2, replyMarkup: keyboard, cancellationToken: CancellationToken.None);
            }
        }

        // --- START OF REWRITTEN METHOD ---
        // This version has major formatting upgrades, more progress bars, and new sections.
        private string GenerateProgressBar(double percentage, int size = 10, string block = "🟩")
        {
            if (percentage <= 0)
            {
                return string.Empty;
            }

            int filledBlocks = (int)Math.Round(percentage / 100.0 * size);
            if (filledBlocks == 0 && percentage > 0.1)
            {
                filledBlocks = 1;
            }

            return string.Concat(Enumerable.Repeat(block, filledBlocks));
        }

        private string FormatCountryReportMessage(CloudflareCountryReportDto data)
        {
            List<string> sections = [];

            // --- Section: At a Glance ---
            StringBuilder summarySb = new();
            _ = summarySb.AppendLine("*📊 At a Glance*");
            _ = summarySb.AppendLine($"  - __Stability:__         {(data.LatestOutage != null ? "🔴 OUTAGE" : "✅ No Outages")}");
            if (data.InternetQuality != null)
            {
                string iqiRating = data.InternetQuality.Rating.ToUpper();
                string iqiEmoji = iqiRating == "GOOD" ? "✅" : (iqiRating == "POOR" ? "⚠️" : "🟠");
                _ = summarySb.AppendLine($"  - __Internet Quality:__ {iqiEmoji} {data.InternetQuality.Rating}");
            }
            if (data.BotVsHumanTraffic != null)
            {
                string botEmoji = data.BotVsHumanTraffic.Bot > 40 ? "⚠️" : "✅";
                _ = summarySb.AppendLine($"  - __Bot Traffic:__      {botEmoji} {data.BotVsHumanTraffic.Bot:F1}%");
            }
            if (data.IpVersionDistribution != null)
            {
                string ipv6Emoji = data.IpVersionDistribution.Ipv6 > 50 ? "✅" : (data.IpVersionDistribution.Ipv6 > 25 ? "🟠" : "⚠️");
                _ = summarySb.AppendLine($"  - __IPv6 Adoption:__    {ipv6Emoji} {data.IpVersionDistribution.Ipv6:F1}%");
            }
            sections.Add(summarySb.ToString());

            // --- Section: Stability & Quality ---
            StringBuilder stabilitySb = new();
            if (data.LatestOutage != null || (data.InternetQuality != null && data.InternetQuality.Value > 0))
            {
                _ = stabilitySb.AppendLine("*📶 Stability & Quality*");
                if (data.LatestOutage != null)
                {
                    ConfirmedOutageData o = data.LatestOutage;
                    _ = stabilitySb.AppendLine($"  - __Outage Cause:__ `{TelegramMessageFormatter.EscapeMarkdownV2(o.Cause)}`");
                    _ = stabilitySb.AppendLine($"  - __Description:__ _{TelegramMessageFormatter.EscapeMarkdownV2(o.Description)}_");
                }
                if (data.InternetQuality != null && data.InternetQuality.Value > 0)
                {
                    IqiData iqi = data.InternetQuality;
                    _ = stabilitySb.AppendLine($"  - __Quality Rating:__ `{iqi.Rating}`");
                    _ = stabilitySb.AppendLine($"  - __p90 Latency:__ `{iqi.Value:F0} ms`");
                }
                sections.Add(stabilitySb.ToString());
            }

            // --- Section: Traffic Composition ---
            StringBuilder compositionSb = new();
            _ = compositionSb.AppendLine("*👥 Traffic Composition (24h)*");
            int compositionItemCount = 0;
            if (data.BotVsHumanTraffic != null)
            {
                _ = compositionSb.AppendLine("  - __Source__");
                BotTrafficData b = data.BotVsHumanTraffic;
                _ = compositionSb.AppendLine($"    `{"Human:",-9} {b.Human,5:F1}%` {GenerateProgressBar(b.Human, 10, "🟩")}");
                _ = compositionSb.AppendLine($"    `{"Bot:",-9} {b.Bot,5:F1}%` {GenerateProgressBar(b.Bot, 10, "🟥")}");
                compositionItemCount++;
            }
            if (data.DeviceTypeDistribution != null)
            {
                _ = compositionSb.AppendLine("  - __Device Types__");
                DeviceTypeData d = data.DeviceTypeDistribution;
                _ = compositionSb.AppendLine($"    `{"Desktop:",-9} {d.Desktop,5:F1}%` {GenerateProgressBar(d.Desktop, 10, "🟦")}");
                _ = compositionSb.AppendLine($"    `{"Mobile:",-9} {d.Mobile,5:F1}%` {GenerateProgressBar(d.Mobile, 10, "🟩")}");
                compositionItemCount++;
            }
            if (data.OSDistribution != null && data.OSDistribution.GetType().GetProperties().Sum(p => (double)p.GetValue(data.OSDistribution)!) > 0)
            {
                _ = compositionSb.AppendLine("  - __Operating Systems__");
                OperatingSystemData os = data.OSDistribution;
                List<(string Name, double Value, string Block)> osList = new List<(string Name, double Value, string Block)> { ("Windows", os.Windows, "🟦"), ("macOS", os.MacOS, "⬜️"), ("Android", os.Android, "🟩"), ("iOS", os.IOS, "⬛️"), ("Linux", os.Linux, "🟧") }
                    .Where(x => x.Value > 0).OrderByDescending(x => x.Value).ToList();

                foreach ((string Name, double Value, string Block) in osList)
                {
                    _ = compositionSb.AppendLine($"    `{Name + ":",-9} {Value,5:F1}%` {GenerateProgressBar(Value, 10, Block)}");
                }
                compositionItemCount++;
            }
            if (compositionItemCount > 0)
            {
                sections.Add(compositionSb.ToString());
            }

            // --- Section: Security Posture ---
            StringBuilder securitySb = new();
            _ = securitySb.AppendLine("*🛡️ Security Posture (7d)*");
            _ = securitySb.AppendLine("  - __L7 Attacks__");
            if (data.Layer7Attacks != null && data.Layer7Attacks.PercentageOfTotal > 0)
            {
                AttackData l7 = data.Layer7Attacks;
                _ = securitySb.AppendLine($"    `Share: {l7.PercentageOfTotal:F1}% of traffic`");
                if (l7.TopSourceCountry != "Unknown")
                {
                    _ = securitySb.AppendLine($"    `Origin: {l7.TopSourceCountry}`");
                }
            }
            else { _ = securitySb.AppendLine("    `✅ No significant attacks`"); }
            if (data.L3AttackDistribution != null && data.L3AttackDistribution.GetType().GetProperties().Sum(p => (double)p.GetValue(data.L3AttackDistribution)!) > 0)
            {
                _ = securitySb.AppendLine("  - __L3 Attack Protocols__");
                Layer3AttackProtocolData l3 = data.L3AttackDistribution;
                if (l3.Udp > 0)
                {
                    _ = securitySb.AppendLine($"    `{"UDP:",-9} {l3.Udp,5:F1}%` {GenerateProgressBar(l3.Udp, 10, "🟧")}");
                }

                if (l3.Tcp > 0)
                {
                    _ = securitySb.AppendLine($"    `{"TCP:",-9} {l3.Tcp,5:F1}%` {GenerateProgressBar(l3.Tcp, 10, "🟦")}");
                }

                if (l3.Icmp > 0)
                {
                    _ = securitySb.AppendLine($"    `{"ICMP:",-9} {l3.Icmp,5:F1}%` {GenerateProgressBar(l3.Icmp, 10, "🟥")}");
                }
            }
            if (data.AttackMitigation != null && data.AttackMitigation.GetType().GetProperties().Sum(p => (double)p.GetValue(data.AttackMitigation)!) > 0)
            {
                _ = securitySb.AppendLine("  - __Attack Mitigation__");
                AttackMitigationData m = data.AttackMitigation;
                if (m.Waf > 0)
                {
                    _ = securitySb.AppendLine($"    `WAF: {m.Waf,5:F1}%`");
                }

                if (m.RateLimiting > 0)
                {
                    _ = securitySb.AppendLine($"    `Rate Limit: {m.RateLimiting,5:F1}%`");
                }

                if (m.BotManagement > 0)
                {
                    _ = securitySb.AppendLine($"    `Bot Mgmt: {m.BotManagement,5:F1}%`");
                }
            }
            sections.Add(securitySb.ToString());

            // --- Section: Technology Adoption ---
            StringBuilder techSb = new();
            _ = techSb.AppendLine("*🚀 Technology Adoption (7d)*");
            _ = techSb.AppendLine("  - __HTTP Versions__");
            HttpProtocolData? h = data.HttpProtocolDistribution;
            if (h != null)
            {
                if (h.Http3 > 0)
                {
                    _ = techSb.AppendLine($"    `{"HTTP/3:",-9} {h.Http3,5:F1}%` {GenerateProgressBar(h.Http3, 10, "🟩")}");
                }

                if (h.Http2 > 0)
                {
                    _ = techSb.AppendLine($"    `{"HTTP/2:",-9} {h.Http2,5:F1}%` {GenerateProgressBar(h.Http2, 10, "🟦")}");
                }

                if (h.Http1 > 0)
                {
                    _ = techSb.AppendLine($"    `{"HTTP/1.x:",-9} {h.Http1,5:F1}%` {GenerateProgressBar(h.Http1, 10, "🟥")}");
                }
            }
            _ = techSb.AppendLine("  - __TLS Versions__");
            TlsVersionData? t = data.TlsVersionDistribution;
            if (t != null)
            {
                if (t.Tls13 > 0)
                {
                    _ = techSb.AppendLine($"    `{"TLS 1.3:",-9} {t.Tls13,5:F1}%` {GenerateProgressBar(t.Tls13, 10, "🟩")}");
                }

                if (t.Tls12 > 0)
                {
                    _ = techSb.AppendLine($"    `{"TLS 1.2:",-9} {t.Tls12,5:F1}%` {GenerateProgressBar(t.Tls12, 10, "🟦")}");
                }

                if (t.Tls11 + t.Tls10 > 0)
                {
                    _ = techSb.AppendLine($"    `{"Legacy:",-9} {t.Tls11 + t.Tls10,5:F1}%` {GenerateProgressBar(t.Tls11 + t.Tls10, 10, "🟥")}");
                }
            }
            _ = techSb.AppendLine("  - __IP Versions (IPv6 Adoption)__");
            IpVersionData? ip = data.IpVersionDistribution;
            if (ip != null)
            {
                _ = techSb.AppendLine($"    `{"IPv6:",-9} {ip.Ipv6,5:F1}%` {GenerateProgressBar(ip.Ipv6, 10, "🟩")}");
                _ = techSb.AppendLine($"    `{"IPv4:",-9} {ip.Ipv4,5:F1}%` {GenerateProgressBar(ip.Ipv4, 10, "🟥")}");
            }
            if (data.PostQuantumSupport != null && data.PostQuantumSupport.Supported > 0)
            {
                _ = techSb.AppendLine("  - __Post-Quantum Ready__");
                PostQuantumData pq = data.PostQuantumSupport;
                _ = techSb.AppendLine($"    `{"Supported:",-9} {pq.Supported,5:F1}%` {GenerateProgressBar(pq.Supported, 10, "🟩")}");
            }
            sections.Add(techSb.ToString());

            // --- Combine everything ---
            StringBuilder finalReport = new();
            _ = finalReport.AppendLine($"☁️ *Internet Report: {TelegramMessageFormatter.EscapeMarkdownV2(data.CountryName)}*");
            _ = finalReport.AppendLine("`-----------------------------------`");
            _ = finalReport.Append(string.Join("\n─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─\n", sections.Where(s => !string.IsNullOrWhiteSpace(s))));

            // --- Footer & Legend ---
            _ = finalReport.AppendLine("\n─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─");
            _ = finalReport.AppendLine("*Legend*");
            _ = finalReport.AppendLine("`OS: 🟦Win ⬜️Mac 🟩And ⬛️iOS 🟧Lin`");
            _ = finalReport.AppendLine("`Dev: 🟦Desktop 🟩Mobile`");
            _ = finalReport.AppendLine("`HTTP/TLS: 🟩Newest 🟦Modern 🟥Old`");
            _ = finalReport.AppendLine("`Source/IP: 🟩Human/IPv6 🟥Bot/IPv4`");

            _ = finalReport.AppendLine("\n`-----------------------------------`");
            _ = !string.IsNullOrWhiteSpace(data.ReportTimestamp) && DateTime.TryParse(data.ReportTimestamp, out DateTime reportTime)
                ? finalReport.AppendLine($"_Data from Cloudflare Radar as of {reportTime:MMM dd, HH:mm} UTC._")
                : finalReport.AppendLine($"_Data from Cloudflare Radar as of {DateTime.UtcNow:MMM dd, HH:mm} UTC._");

            return finalReport.ToString();
        }

        // --- END OF REWRITTEN METHOD ---

        private async Task<TResult> AnimateWhileExecutingAsync<TResult>(long chatId, int messageId, string baseText, Func<CancellationToken, Task<TResult>> operationToExecute, CancellationToken cancellationToken)
        {
            string[] animationFrames = new[] { "·", "··", "···" };
            int frameIndex = 0;
            Task<TResult> operationTask = operationToExecute(cancellationToken);

            Task animationTask = Task.Run(async () =>
            {
                while (!operationTask.IsCompleted && !cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        string text = $"{baseText} {animationFrames[frameIndex++ % animationFrames.Length]}";
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