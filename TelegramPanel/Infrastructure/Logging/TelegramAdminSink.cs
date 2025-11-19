// --- File: Infrastructure/Logging/TelegramAdminSink.cs (V8.5-RESILIENT) ---

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
        private readonly AsyncRetryPolicy _retryPolicy;
        private readonly SinkState _sinkState;
        private readonly TelegramMessageBuilder _messageBuilder;
        private readonly TelegramBotClient? _botClient; // Nullable to handle init failure gracefully

        public TelegramAdminSink(IConfiguration configuration)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _sinkState = new SinkState();

            Console.WriteLine("\n╔═════════════════════════════════════╗");
            Console.WriteLine("║   Ultimate V8.5 Telegram Sink Init  ║");
            Console.WriteLine("╚═════════════════════════════════════╝");

            _botToken = _configuration["TelegramPanel:BotToken"] ?? string.Empty;
            _adminChatIds = _configuration.GetSection("TelegramPanel:AdminUserIds").Get<List<long>>() ?? [];

            string? dashboardUrl = _configuration["TelegramPanel:DashboardUrl"];
            // Safe handling if dashboardUrl is null
            _messageBuilder = new TelegramMessageBuilder(dashboardUrl ?? "http://localhost:5000");

            // --- FIX: Validation logic to prevent crash on startup ---
            bool isTokenValid = !string.IsNullOrWhiteSpace(_botToken)
                                && !_botToken.Contains("REPLACE")
                                && !_botToken.Contains("123456"); // Detect CI/CD placeholder

            if (isTokenValid && _adminChatIds.Any())
            {
                try
                {
                    // Initialize client inside try-catch to prevent app crash if token format is bad
                    _botClient = new TelegramBotClient(_botToken);
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("[SINK V8.5] Status: ✅ CONFIG OK");
                }
                catch (Exception ex)
                {
                    _botClient = null;
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"[SINK V8.5] Warning: Bot client init failed ({ex.Message}). Sink disabled.");
                }
            }
            else
            {
                _botClient = null;
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("[SINK V8.5] Status: ⚠️ DISABLED (Invalid Config or Test Mode)");
            }
            Console.ResetColor();
            // ----------------------------------------------------------

            _retryPolicy = Policy
                .Handle<Exception>()
                .WaitAndRetryAsync(3, _ => TimeSpan.FromSeconds(5), (ex, ts, attempt, _) =>
                {
                    // Reduced log noise for retries
                    // Console.WriteLine($"[SINK RETRY] Attempt {attempt} failed..."); 
                });
        }

        public void Emit(LogEvent logEvent)
        {
            // FIX: Early exit if client failed to initialize
            if (_botClient == null || !_adminChatIds.Any())
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
            if (_botClient == null) return;

            try
            {
                (string? messageText, Telegram.Bot.Types.ReplyMarkups.ReplyMarkup? keyboard) = _messageBuilder.Build(logEvent, occurrenceCount);

                // Optional: logic to build attachment if needed (commented out in original)
                // InputFileStream? exceptionAttachment = BuildExceptionAttachment(logEvent);

                foreach (long adminId in _adminChatIds)
                {
                    await _retryPolicy.ExecuteAsync(async () =>
                    {
                        if (messageText != null)
                        {
                            await _botClient.SendMessage(
                                chatId: adminId,
                                text: messageText,
                                parseMode: ParseMode.Markdown,
                                replyMarkup: keyboard
                            );
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                // Fallback logging to file if Telegram fails
                try
                {
                    string errorMessage = $"[SINK ERROR] {DateTime.UtcNow:O} - Failed to send log: {ex.Message}";
                    // Avoid Console.WriteLine here to prevent log loops if console is piped to Serilog
                    await File.AppendAllTextAsync("telegram_sink_errors.log", errorMessage + Environment.NewLine);
                }
                catch { /* Ignore file write errors */ }
            }
        }

        private InputFileStream? BuildExceptionAttachment(LogEvent logEvent)
        {
            if (logEvent.Exception is null)
            {
                return null;
            }

            StringBuilder sb = new();
            _ = sb.AppendLine("--- 🗂️ ULTIMATE DEBUG ATTACHMENT ---");
            _ = sb.AppendLine($"Timestamp (UTC): {logEvent.Timestamp:O}");
            _ = sb.AppendLine(new string('=', 50));
            _ = sb.AppendLine("--- LOG PROPERTIES ---");

            foreach (KeyValuePair<string, LogEventPropertyValue> prop in logEvent.Properties)
            {
                _ = sb.AppendLine($"🔹 {prop.Key}: {prop.Value}");
            }

            _ = sb.AppendLine(new string('=', 50));
            _ = sb.AppendLine("--- EXCEPTION TRACE ---");
            _ = sb.AppendLine(logEvent.Exception.ToString());

            byte[] bytes = Encoding.UTF8.GetBytes(sb.ToString());
            return new InputFileStream(new MemoryStream(bytes), $"Exception_{logEvent.Timestamp:yyyyMMdd_HHmmss}.txt");
        }
    }
}