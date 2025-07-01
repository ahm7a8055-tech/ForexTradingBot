// --- File: Infrastructure/Logging/TelegramAdminSink.cs (V8.3 - FINAL FIX) ---

using Microsoft.Extensions.Configuration;
using Polly;
using Polly.Retry;
using Serilog.Core;
using Serilog.Events;
using System.Text;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace TelegramPanel.Infrastructure.Logging
{
    public class TelegramAdminSink : ILogEventSink
    {
        private readonly IConfiguration _configuration;
        private readonly string _botToken;
        private readonly List<long> _adminChatIds;
        private readonly AsyncRetryPolicy _telegramRetryPolicy;
        private readonly SinkState _sinkState;
        private readonly TelegramMessageBuilder _messageBuilder;
        private readonly bool _sendMetricsReport;
        public TelegramAdminSink(IConfiguration configuration)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _sinkState = new SinkState();

            Console.WriteLine("\n--- ╔═════════════════════════════════════╗ ---");
            Console.WriteLine("--- ║   Ultimate V8.3 Telegram Sink Init  ║ ---");
            Console.WriteLine("--- ╚═════════════════════════════════════╝ ---");
            _botToken = _configuration["TelegramPanel:BotToken"] ?? string.Empty;
            _adminChatIds = _configuration.GetSection("TelegramPanel:AdminUserIds").Get<List<long>>() ?? [];
            string? dashboardUrl = _configuration["TelegramPanel:DashboardUrl"];

            _messageBuilder = new TelegramMessageBuilder(dashboardUrl);

            bool isConfigOk = !string.IsNullOrEmpty(_botToken) && _adminChatIds.Any();
            Console.ForegroundColor = isConfigOk ? ConsoleColor.Green : ConsoleColor.Red;
            Console.WriteLine($"[SINK V8.3] Status: {(isConfigOk ? "✅ CONFIGURATION OK" : "❌ CONFIGURATION FAILED")}");
            Console.ResetColor();

            _telegramRetryPolicy = Policy.Handle<Exception>()
        .WaitAndRetryAsync(
            retryCount: 3, // Keep the number of retries
            sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(15), // <-- FIXED 15-SECOND WAIT BETWEEN RETRIES
            onRetry: (exception, timespan, retryAttempt, context) => // Note: 'onRetry' instead of 'onRetryAsync' if not async
            {
                // This delegate is called *before* the wait, but it's good for logging the intent.
                // The actual wait happens *after* this delegate returns.
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine($"[SINK V8.4 RETRY] Attempt {retryAttempt} failed: {exception.Message}. Waiting 15 seconds before next attempt...");
                Console.ResetColor();
                // If the retry logic itself needs to be async, use WaitAndRetryAsync and onRetryAsync.
                // For a simple fixed delay, the above is sufficient.
                // If you *need* async within onRetry, use WaitAndRetryAsync and onRetryAsync.
            });
        }

        public void Emit(LogEvent logEvent)
        {
            if (string.IsNullOrEmpty(_botToken) || !_adminChatIds.Any())
            {
                return;
            }

            if (_sinkState.ShouldThrottle(logEvent, out int occurrenceCount))
            {
                return;
            }

            _ = Task.Run(() => SendNotificationAsync(logEvent, occurrenceCount));
        }

        private async Task SendNotificationAsync(LogEvent logEvent, int occurrenceCount)
        {
            Console.WriteLine($"[SINK V8.3 DEBUG] Entering SendNotificationAsync for event: {logEvent.MessageTemplate.Text}");
            try
            {
                TelegramBotClient botClient = new(_botToken);
                (string message, Telegram.Bot.Types.ReplyMarkups.ReplyMarkup keyboard) = _messageBuilder.Build(logEvent, occurrenceCount);
                InputFileStream? exceptionFile = BuildExceptionAttachment(logEvent);

                foreach (long adminId in _adminChatIds)
                {
                    await _telegramRetryPolicy.ExecuteAsync(async () =>
                    {
                        _ = await botClient.SendMessage(chatId: adminId, text: message, parseMode: ParseMode.Markdown, replyMarkup: keyboard, cancellationToken: CancellationToken.None);
                        // if (exceptionFile != null)
                        {
                            //     exceptionFile.Content.Position = 0;
                            //    await botClient.SendDocument(chatId: adminId, document: exceptionFile, caption: "🗂️ Ultimate debug attachment.", cancellationToken: CancellationToken.None);
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                string errorMessage = $"[SINK V8.3 CRITICAL FAILURE] {DateTime.UtcNow:O} | Could not send notification. Error: {ex}";
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(errorMessage);
                Console.ResetColor();
                await File.AppendAllTextAsync("telegram_sink_errors.log", errorMessage + Environment.NewLine);
            }
        }
        private string BuildMetricsReport(string rawReportText)
        {
            StringBuilder sb = new();
            _ = sb.AppendLine("<b>📊 Queue Metrics Report</b>");
            _ = sb.AppendLine(); // Add a blank line for spacing

            // The <pre> HTML tag preserves whitespace and uses a monospaced font,
            // which is perfect for displaying reports exactly as they appear in the console.
            _ = sb.Append("<pre>");
            _ = sb.Append(System.Net.WebUtility.HtmlEncode(rawReportText)); // IMPORTANT: Encode to prevent any HTML conflicts
            _ = sb.Append("</pre>");

            return sb.ToString();
        }

        private InputFileStream? BuildExceptionAttachment(LogEvent logEvent)
        {
            if (logEvent.Exception == null)
            {
                return null;
            }

            StringBuilder sb = new();
            _ = sb.AppendLine("--- 🗂️ ULTIMATE DEBUG ATTACHMENT ---");
            _ = sb.AppendLine($"Timestamp (UTC): {logEvent.Timestamp:O}");
            _ = sb.AppendLine(new string('=', 50));
            _ = sb.AppendLine("--- ALL LOG PROPERTIES ---");
            foreach (KeyValuePair<string, LogEventPropertyValue> prop in logEvent.Properties)
            {
                // --- THIS IS THE FIX ---
                // We remove the invalid "l" format specifier. The default ToString() is safe.
                _ = sb.AppendLine($"🔹 {prop.Key}: {prop.Value}");
            }
            _ = sb.AppendLine(new string('=', 50));
            _ = sb.AppendLine("--- FULL EXCEPTION STACK TRACE ---");
            _ = sb.AppendLine(logEvent.Exception.ToString());

            byte[] exceptionBytes = Encoding.UTF8.GetBytes(sb.ToString());
            return new InputFileStream(new MemoryStream(exceptionBytes), $"Exception_{logEvent.Timestamp:yyyyMMdd_HHmmss}.txt");
        }
    }
}