// --- START OF FULLY CORRECTED FILE: ForceJoinSettingsCallbackHandler.cs ---

using Application.DTOs.Settings;
using Application.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text;
using Telegram.Bot; // <-- ADD THIS for ITelegramBotClient
using Telegram.Bot.Exceptions; // <-- ADD THIS for ApiRequestException
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using TelegramPanel.Application.Interfaces;
using TelegramPanel.Application.States;
using TelegramPanel.Formatters;
using TelegramPanel.Infrastructure;
using TelegramPanel.Infrastructure.Helper;
using TelegramPanel.Settings;
using static TelegramPanel.Infrastructure.ActualTelegramMessageActions;

namespace TelegramPanel.Application.CommandHandlers.Admin
{
    /// <summary>
    /// Manages the UI for the Force Join settings in the admin panel.
    /// </summary>
    public class ForceJoinSettingsCallbackHandler : ITelegramCallbackQueryHandler
    {
        // --- 1. DECLARE THE FIELDS ---
        private readonly ILogger<ForceJoinSettingsCallbackHandler> _logger;
        private readonly ITelegramMessageSender _messageSender;
        private readonly IAdminService _adminService;
        private readonly ITelegramStateMachine _stateMachine;
        private readonly ITelegramBotClient _botClient; // <-- FIX: Field is now declared
        private readonly TelegramPanelSettings _settings;

        // Constants
        private const string MenuCallback = "admin_forcejoin_menu";
        private const string ToggleCallback = "admin_forcejoin_toggle";
        private const string SetChannelCallback = "admin_forcejoin_set_channel";
        private const string SetMessageCallback = "admin_forcejoin_set_message";

        // --- 2. UPDATE THE CONSTRUCTOR ---
        public ForceJoinSettingsCallbackHandler(
            ILogger<ForceJoinSettingsCallbackHandler> logger,
            ITelegramMessageSender messageSender,
            IAdminService adminService,
            ITelegramStateMachine stateMachine,
            ITelegramBotClient botClient, // <-- FIX: Inject the bot client
            IOptions<TelegramPanelSettings> settingsOptions)
        {
            _logger = logger;
            _messageSender = messageSender;
            _adminService = adminService;
            _stateMachine = stateMachine;
            _botClient = botClient; // <-- FIX: Assign the injected client to the field
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

            var handlerTask = action switch
            {
                SetChannelCallback => _stateMachine.SetStateAsync(chatId, "WaitingForForceJoinChannel", update, cancellationToken),
                SetMessageCallback => _stateMachine.SetStateAsync(chatId, "WaitingForForceJoinMessage", update, cancellationToken),
                _ => HandleMenuActions(action, chatId, messageId, cancellationToken)
            };
            await handlerTask;
        }

        private async Task HandleMenuActions(string action, long chatId, int messageId, CancellationToken cancellationToken)
        {
            if (action == ToggleCallback)
            {
                var currentSettings = await _adminService.GetForceJoinSettingsAsync(cancellationToken);
                currentSettings.IsEnabled = !currentSettings.IsEnabled;
                await _adminService.UpdateForceJoinSettingsAsync(currentSettings, cancellationToken);
            }

            await ShowForceJoinMenuAsync(chatId, messageId, cancellationToken);
        }

        private async Task ShowForceJoinMenuAsync(long chatId, int messageId, CancellationToken cancellationToken)
        {
            var settings = await _adminService.GetForceJoinSettingsAsync(cancellationToken);
            var text = new StringBuilder();

            text.AppendLine(TelegramMessageFormatter.Bold("🛂 Force Join Settings"));
            text.AppendLine();
            text.AppendLine($"**Status:** {(settings.IsEnabled ? "✅ Enabled" : "❌ Disabled")}");

            if (settings.ChannelId != 0)
            {
                try
                {
                    // This code now works because _botClient exists
                    var chatInfo = await _botClient.GetChat(settings.ChannelId, cancellationToken);
                    var memberCount = await _botClient.GetChatMemberCount(settings.ChannelId, cancellationToken);

                    text.AppendLine($"**Title:** {TelegramMessageFormatter.EscapeMarkdownV2(chatInfo.Title)}");
                    text.AppendLine($"**ID:** `{chatInfo.Id}`");
                    text.AppendLine($"**Link:** {TelegramMessageFormatter.EscapeMarkdownV2(settings.ChannelLink)}");
                    text.AppendLine($"**Members:** {memberCount:N0}");
                }
                catch (ApiRequestException ex)
                {
                    _logger.LogWarning(ex, "Failed to fetch details for configured Force Join channel {ChannelId}.", settings.ChannelId);
                    text.AppendLine($"**Channel ID:** `{settings.ChannelId}`");
                    text.AppendLine($"**Status:** ⚠️ **Error Fetching Details**");
                    text.AppendLine($"**Reason:** _{TelegramMessageFormatter.EscapeMarkdownV2(ex.Message)}_");
                }
            }
            else
            {
                text.AppendLine("**Channel:** Not Set");
            }

            text.AppendLine();
            text.AppendLine("**Message:**");

            if (!string.IsNullOrWhiteSpace(settings.Message))
            {
                text.AppendLine(TelegramMessageFormatter.Bold(settings.Message));
            }
            else
            {
                text.AppendLine("_Default message will be used._");
            }

            var keyboard = GetKeyboard(settings);

            await _messageSender.EditMessageTextAsync(
                chatId: chatId,
                messageId: messageId,
                text: text.ToString(),
                parseMode: ParseMode.MarkdownV2,
                replyMarkup: keyboard,
                cancellationToken: cancellationToken);
        }

        private InlineKeyboardMarkup GetKeyboard(ForceJoinSettingsDto settings)
        {
            var toggleButton = settings.IsEnabled
                ? InlineKeyboardButton.WithCallbackData("❌ Disable Feature", ToggleCallback)
                : InlineKeyboardButton.WithCallbackData("✅ Enable Feature", ToggleCallback);

            var setChannelButton = InlineKeyboardButton.WithCallbackData("✏️ Set Channel", SetChannelCallback);
            var setMessageButton = InlineKeyboardButton.WithCallbackData("📝 Set Message", SetMessageCallback);

            var backButton = InlineKeyboardButton.WithCallbackData("⬅️ Back to Settings", "admin_bot_settings");

            // --- THIS IS THE FIX ---
            // Instead of creating an array of arrays (new[] { new[] { ... } }),
            // we create a List of Lists (new List<List<...>>).
            // This solves the Hangfire deserialization issue.
            return new InlineKeyboardMarkup(new List<List<InlineKeyboardButton>>
    {
        // Row 1
        new List<InlineKeyboardButton> { toggleButton },
        
        // Row 2
        new List<InlineKeyboardButton> { setChannelButton, setMessageButton },
        
        // Row 3
        new List<InlineKeyboardButton> { backButton }
    });
        }
    }
}