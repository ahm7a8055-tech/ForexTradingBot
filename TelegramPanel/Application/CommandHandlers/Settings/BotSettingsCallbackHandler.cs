// --- START OF NEW FILE: BotSettingsCallbackHandler.cs ---
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using TelegramPanel.Application.Interfaces;
using TelegramPanel.Formatters;
using TelegramPanel.Infrastructure.Helper;
using TelegramPanel.Settings;
// Note: You need a constant for "admin_panel_main" or to make it public in AdminCallbackHandler
using static TelegramPanel.Infrastructure.ActualTelegramMessageActions;

namespace TelegramPanel.Application.CommandHandlers.Settings
{
    /// <summary>
    /// Handles callbacks for the main "Bot Settings" menu in the admin panel.
    /// </summary>
    public class BotSettingsCallbackHandler : ITelegramCallbackQueryHandler
    {
        private readonly ILogger<BotSettingsCallbackHandler> _logger;
        private readonly ITelegramMessageSender _messageSender;
        private readonly TelegramPanelSettings _settings;

        // The specific callback this handler is responsible for.
        private const string BotSettingsCallback = "admin_bot_settings";

        public BotSettingsCallbackHandler(
            ILogger<BotSettingsCallbackHandler> logger,
            ITelegramMessageSender messageSender,
            IOptions<TelegramPanelSettings> settingsOptions)
        {
            _logger = logger;
            _messageSender = messageSender;
            _settings = settingsOptions.Value;
        }

        public bool CanHandle(Update update)
        {
            // Only handle this specific callback from an admin user.
            return update.Type == UpdateType.CallbackQuery &&
                   update.CallbackQuery?.Data == BotSettingsCallback &&
                   _settings.AdminUserIds.Contains(update.CallbackQuery.From.Id);
        }

        public async Task HandleAsync(Update update, CancellationToken cancellationToken = default)
        {
            CallbackQuery callbackQuery = update.CallbackQuery!;
            await _messageSender.AnswerCallbackQueryAsync(callbackQuery.Id, cancellationToken: cancellationToken);
            _logger.LogInformation("Admin {UserId} accessed bot settings menu.", callbackQuery.From.Id);

            string text = TelegramMessageFormatter.Bold("⚙️ Bot Settings");
            text += "\n\nSelect a setting to configure:";

            InlineKeyboardMarkup? keyboard = MarkupBuilder.CreateInlineKeyboard(
                new[] // Row 1: Feature Settings
                {
                    InlineKeyboardButton.WithCallbackData("🛂 Force Join", "admin_forcejoin_menu")
                },
                // You can add more settings buttons here in the future
                new[] // Row 2: Navigation
                {
                    // You might need to make BackToAdminPanelCallback public or move it to a shared constants file
                    InlineKeyboardButton.WithCallbackData("⬅️ Back to Admin Panel", "admin_panel_main")
                });

            await _messageSender.EditMessageTextAsync(
                chatId: callbackQuery.Message!.Chat.Id,
                messageId: callbackQuery.Message.MessageId,
                text: text,
                parseMode: ParseMode.Markdown,
                replyMarkup: keyboard,
                cancellationToken: cancellationToken);
        }
    }
}