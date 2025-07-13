using Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shared.Settings;
using System.Text;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace Infrastructure.Services
{
    /// <summary>
    /// Implements the admin notification service using the Telegram Bot API.
    /// This version includes logic to automatically split messages that exceed Telegram's character limit.
    /// </summary>
    public class NotificationToAdminService : INotificationToAdminService
    {
        // Telegram's official limit is 4096. We use a slightly smaller value for a safety margin.
        private const int MaxTelegramMessageLength = 4000;

        private readonly ILogger<NotificationToAdminService> _logger;
        private readonly AdminNotificationSettings _settings;
        private readonly ITelegramBotClient _telegramBotClient;

        public NotificationToAdminService(
            ILogger<NotificationToAdminService> logger,
            IOptions<AdminNotificationSettings> settings,
            ITelegramBotClient telegramBotClient)
        {
            _logger = logger;
            _settings = settings.Value;
            _telegramBotClient = telegramBotClient;
        }

        /// <inheritdoc />
        public async Task SendNotificationAsync(string message, CancellationToken cancellationToken)
        {
            if (_settings.AdminChatId == 0)
            {
                _logger.LogWarning("AdminChatId is not configured. Cannot send admin notification.");
                return;
            }

            if (string.IsNullOrWhiteSpace(message))
            {
                _logger.LogDebug("Attempted to send an empty or null notification to admin. Aborting.");
                return;
            }

            // If the message is within the limit, send it directly.
            if (message.Length <= MaxTelegramMessageLength)
            {
                await SendSingleMessageAsync(message, cancellationToken);
            }
            else
            {
                // If the message is too long, split it into chunks and send them sequentially.
                _logger.LogInformation("Message exceeds {Limit} characters. Splitting into multiple parts.", MaxTelegramMessageLength);
                await SendSplitMessageAsync(message, cancellationToken);
            }
        }

        /// <summary>
        /// Sends a single message without splitting.
        /// </summary>
        private async Task SendSingleMessageAsync(string message, CancellationToken cancellationToken)
        {
            try
            {
                await _telegramBotClient.SendMessage(
                    chatId: _settings.AdminChatId,
                    text: message,
                    parseMode: ParseMode.Markdown,
                    cancellationToken: cancellationToken
                );
                _logger.LogInformation("Successfully sent single-part notification to admin.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send single-part notification to admin with Chat ID {AdminChatId}", _settings.AdminChatId);
            }
        }

        /// <summary>
        /// Splits a long message into multiple parts and sends them with headers.
        /// </summary>
        private async Task SendSplitMessageAsync(string longMessage, CancellationToken cancellationToken)
        {
            try
            {
                var chunks = SplitMessageIntoChunks(longMessage);
                int totalChunks = chunks.Count;

                for (int i = 0; i < totalChunks; i++)
                {
                    // Create a header for each part, e.g., "(Part 1/3)"
                    string header = $"**(Part {i + 1}/{totalChunks})**\n\n";
                    string messagePart = header + chunks[i];

                    await _telegramBotClient.SendMessage(
                        chatId: _settings.AdminChatId,
                        text: messagePart,
                        parseMode: ParseMode.Markdown,
                        cancellationToken: cancellationToken
                    );

                    _logger.LogDebug("Sent part {PartNumber}/{TotalParts} of notification to admin.", i + 1, totalChunks);

                    // Add a small delay between messages to ensure they arrive in order and avoid rate limiting.
                    if (i < totalChunks - 1)
                    {
                        await Task.Delay(500, cancellationToken);
                    }
                }
                _logger.LogInformation("Successfully sent multi-part notification ({TotalParts} parts) to admin.", totalChunks);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "A failure occurred while sending a multi-part notification to admin with Chat ID {AdminChatId}", _settings.AdminChatId);
            }
        }

        /// <summary>
        /// A helper method to intelligently split a string into chunks that respect the max length.
        /// It prioritizes splitting at newlines to keep formatting intact.
        /// </summary>
        private static List<string> SplitMessageIntoChunks(string originalMessage)
        {
            var chunks = new List<string>();
            if (string.IsNullOrEmpty(originalMessage)) return chunks;

            // The effective max length for a chunk must account for the header we will add later.
            // e.g., "(Part 99/99)\n\n" is roughly 16 characters. Let's use 30 for safety.
            const int maxChunkLength = MaxTelegramMessageLength - 30;

            var currentChunk = new StringBuilder();
            var lines = originalMessage.Split('\n');

            foreach (var line in lines)
            {
                // If adding the next line would exceed the limit, finish the current chunk.
                if (currentChunk.Length + line.Length + 1 > maxChunkLength)
                {
                    // If the current chunk has content, add it to the list.
                    if (currentChunk.Length > 0)
                    {
                        chunks.Add(currentChunk.ToString());
                        currentChunk.Clear();
                    }
                }

                // If a single line is too long, it must be split forcefully.
                if (line.Length > maxChunkLength)
                {
                    int offset = 0;
                    while (offset < line.Length)
                    {
                        int lengthToTake = Math.Min(maxChunkLength, line.Length - offset);
                        chunks.Add(line.Substring(offset, lengthToTake));
                        offset += lengthToTake;
                    }
                }
                else
                {
                    currentChunk.AppendLine(line);
                }
            }

            // Add the final remaining chunk to the list.
            if (currentChunk.Length > 0)
            {
                chunks.Add(currentChunk.ToString());
            }

            return chunks;
        }
    }
}