// --- START OF UPGRADED FILE: ForceJoinSettingsCallbackHandler.cs ---
using Application.DTOs.Settings;
using Application.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using TelegramPanel.Application.Interfaces;
using TelegramPanel.Application.States; // UPGRADED: For ITelegramStateMachine
using TelegramPanel.Formatters;
using TelegramPanel.Infrastructure;
using TelegramPanel.Infrastructure.Helper;
// using TelegramPanel.Models; // REMOVED: No longer needed
using TelegramPanel.Settings;
using static TelegramPanel.Infrastructure.ActualTelegramMessageActions;

namespace TelegramPanel.Application.CommandHandlers.Admin
{
    public class ForceJoinSettingsCallbackHandler : ITelegramCallbackQueryHandler
    {
        private readonly ILogger<ForceJoinSettingsCallbackHandler> _logger;
        private readonly ITelegramMessageSender _messageSender;
        private readonly IAdminService _adminService;
        private readonly ITelegramStateMachine _stateMachine; // UPGRADED: From _stateManager
        private readonly TelegramPanelSettings _settings;

        private const string MenuCallback = "admin_forcejoin_menu";
        private const string ToggleCallback = "admin_forcejoin_toggle";
        private const string SetChannelCallback = "admin_forcejoin_set_channel";
        private const string SetMessageCallback = "admin_forcejoin_set_message";

        public ForceJoinSettingsCallbackHandler(
            ILogger<ForceJoinSettingsCallbackHandler> logger,
            ITelegramMessageSender messageSender,
            IAdminService adminService,
            ITelegramStateMachine stateMachine, // UPGRADED
            IOptions<TelegramPanelSettings> settingsOptions)
        {
            _logger = logger;
            _messageSender = messageSender;
            _adminService = adminService;
            _stateMachine = stateMachine; // UPGRADED
            _settings = settingsOptions.Value;
        }

        public bool CanHandle(Update update)
        {
            return update.Type == UpdateType.CallbackQuery &&
                   update.CallbackQuery?.Data != null &&
                   update.CallbackQuery.Data.StartsWith("admin_forcejoin_") &&
                   _settings.AdminUserIds.Contains(update.CallbackQuery.From.Id);
        }

        public async Task HandleAsync(Update update, CancellationToken cancellationToken = default)
        {
            var callbackQuery = update.CallbackQuery!;
            await _messageSender.AnswerCallbackQueryAsync(callbackQuery.Id, cancellationToken: cancellationToken);
            _logger.LogInformation("Admin {UserId} managed Force Join settings with action: {Action}", callbackQuery.From.Id, callbackQuery.Data);

            var action = callbackQuery.Data;
            var chatId = callbackQuery.Message!.Chat.Id;
            var messageId = callbackQuery.Message.MessageId;

            // --- UPGRADED: Use the new State Machine ---
            var handlerTask = action switch
            {
                SetChannelCallback => _stateMachine.SetStateAsync(chatId, "WaitingForForceJoinChannel", update, cancellationToken),
                SetMessageCallback => _stateMachine.SetStateAsync(chatId, "WaitingForForceJoinMessage", update, cancellationToken),
                _ => HandleMenuActions(action, chatId, messageId, cancellationToken)
            };

            await handlerTask;
        }

        // Helper method to keep HandleAsync clean
        private async Task HandleMenuActions(string action, long chatId, int messageId, CancellationToken cancellationToken)
        {
            if (action == ToggleCallback)
            {
                var currentSettings = await _adminService.GetForceJoinSettingsAsync(cancellationToken);
                currentSettings.IsEnabled = !currentSettings.IsEnabled;
                await _adminService.UpdateForceJoinSettingsAsync(currentSettings, cancellationToken);
            }

            // For toggle and menu refresh, show the updated menu
            await ShowForceJoinMenuAsync(chatId, messageId, cancellationToken);
        }

        private async Task ShowForceJoinMenuAsync(long chatId, int messageId, CancellationToken cancellationToken)
        {
            var settings = await _adminService.GetForceJoinSettingsAsync(cancellationToken);

            var text = new StringBuilder();
            text.AppendLine(TelegramMessageFormatter.Bold("🛂 Force Join Settings"));
            text.AppendLine(); // Add a blank line for spacing
            text.AppendLine($"**Status:** {(settings.IsEnabled ? "✅ Enabled" : "❌ Disabled")}");

            // --- UPGRADED: Display detailed channel info ---
            if (settings.ChannelId != 0)
            {
                // Use Monospace for the ID and the link for clarity
                text.AppendLine($"**Channel ID:** `{settings.ChannelId}`");
                text.AppendLine($"**Channel Link:** `{settings.ChannelLink}`");
            }
            else
            {
                text.AppendLine("**Channel:** Not Set");
            }

            text.AppendLine(); // Add another blank line before the message
            text.AppendLine("**Message:**");

            // Use Monospace block for the message to show it exactly as it will appear
            if (!string.IsNullOrWhiteSpace(settings.Message))
            {
                text.AppendLine(TelegramMessageFormatter.Bold(settings.Message));
            }
            else
            {
                text.AppendLine("_Default message will be used._");
            }

            var keyboard = GetKeyboard(settings);

            // Using MarkdownV2 for better formatting control
            await _messageSender.EditMessageTextAsync(
                chatId: chatId,
                messageId: messageId,
                text: text.ToString(),
                parseMode: ParseMode.Markdown, // Sticking with Markdown as per your original for simplicity
                replyMarkup: keyboard,
                cancellationToken: cancellationToken);
        }
        // This is unchanged
        private InlineKeyboardMarkup GetKeyboard(ForceJoinSettingsDto settings)
        {
            return MarkupBuilder.CreateInlineKeyboard(
                new[]
                {
                    InlineKeyboardButton.WithCallbackData(
                        settings.IsEnabled ? "❌ Disable" : "✅ Enable", ToggleCallback),
                    InlineKeyboardButton.WithCallbackData(
                        "✏️ Set Channel", SetChannelCallback)
                },
                new[]
                {
                     InlineKeyboardButton.WithCallbackData(
                        "📝 Set Message", SetMessageCallback)
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("⬅️ Back to Bot Settings", "admin_bot_settings")
                }
            );
        }
}
}