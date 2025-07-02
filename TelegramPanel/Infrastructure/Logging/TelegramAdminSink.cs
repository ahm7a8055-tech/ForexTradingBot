// --- File: Infrastructure/Logging/TelegramAdminSink.cs (V8.4-CLEAN) ---

using Microsoft.Extensions.Configuration;
using Polly;
using Polly.Retry;
using Serilog.Core;
using Serilog.Events;
using System.Text;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types;
using System.Collections.Concurrent;

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
        private readonly TelegramBotClient _botClient;

        public TelegramAdminSink(IConfiguration configuration)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _sinkState = new SinkState();

            Console.WriteLine("\n╔═════════════════════════════════════╗");
            Console.WriteLine("║   Ultimate V8.4 Telegram Sink Init  ║");
            Console.WriteLine("╚═════════════════════════════════════╝");

            _botToken = _configuration["TelegramPanel:BotToken"] ?? string.Empty;
            _adminChatIds = _configuration.GetSection("TelegramPanel:AdminUserIds").Get<List<long>>() ?? [];

            var dashboardUrl = _configuration["TelegramPanel:DashboardUrl"];
            _messageBuilder = new TelegramMessageBuilder(dashboardUrl);
            _botClient = new TelegramBotClient(_botToken);

            bool configValid = !string.IsNullOrWhiteSpace(_botToken) && _adminChatIds.Any();
            Console.ForegroundColor = configValid ? ConsoleColor.Green : ConsoleColor.Red;
            Console.WriteLine($"[SINK V8.4] Status: {(configValid ? "✅ CONFIG OK" : "❌ CONFIG FAILED")}");
            Console.ResetColor();

            _retryPolicy = Policy
                .Handle<Exception>()
                .WaitAndRetryAsync(3, _ => TimeSpan.FromSeconds(15), (ex, ts, attempt, _) =>
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"[SINK RETRY] Attempt {attempt} failed: {ex.Message}. Retrying in {ts.TotalSeconds}s...");
                    Console.ResetColor();
                });
        }

        public void Emit(LogEvent logEvent)
        {
            if (string.IsNullOrEmpty(_botToken) || !_adminChatIds.Any())
                return;

            if (_sinkState.ShouldThrottle(logEvent, out int occurrenceCount))
                return;

            _ = Task.Run(() => SendNotificationAsync(logEvent, occurrenceCount));
        }

        private async Task SendNotificationAsync(LogEvent logEvent, int occurrenceCount)
        {
            try
            {
                var (messageText, keyboard) = _messageBuilder.Build(logEvent, occurrenceCount);
                var exceptionAttachment = BuildExceptionAttachment(logEvent);

                foreach (var adminId in _adminChatIds)
                {
                    await _retryPolicy.ExecuteAsync(async () =>
                    {
                        await _botClient.SendMessage(
                            chatId: adminId,
                            text: messageText,
                            parseMode: ParseMode.Markdown,
                            replyMarkup: keyboard
                        );

                        // Enable the following if you want to send the exception file
                        /*
                        if (exceptionAttachment != null)
                        {
                            exceptionAttachment.Content.Position = 0;
                            await _botClient.SendDocumentAsync(
                                chatId: adminId,
                                document: exceptionAttachment,
                                caption: "🗂️ Debug attachment"
                            );
                        }
                        */
                    });
                }
            }
            catch (Exception ex)
            {
                var errorMessage = $"[SINK ERROR] {DateTime.UtcNow:O} - Failed to send log: {ex}";
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(errorMessage);
                Console.ResetColor();

                await File.AppendAllTextAsync("telegram_sink_errors.log", errorMessage + Environment.NewLine);
            }
        }

        private InputFileStream? BuildExceptionAttachment(LogEvent logEvent)
        {
            if (logEvent.Exception is null)
                return null;

            var sb = new StringBuilder();
            sb.AppendLine("--- 🗂️ ULTIMATE DEBUG ATTACHMENT ---");
            sb.AppendLine($"Timestamp (UTC): {logEvent.Timestamp:O}");
            sb.AppendLine(new string('=', 50));
            sb.AppendLine("--- LOG PROPERTIES ---");

            foreach (var prop in logEvent.Properties)
                sb.AppendLine($"🔹 {prop.Key}: {prop.Value}");

            sb.AppendLine(new string('=', 50));
            sb.AppendLine("--- EXCEPTION TRACE ---");
            sb.AppendLine(logEvent.Exception.ToString());

            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            return new InputFileStream(new MemoryStream(bytes), $"Exception_{logEvent.Timestamp:yyyyMMdd_HHmmss}.txt");
        }
    }
}
