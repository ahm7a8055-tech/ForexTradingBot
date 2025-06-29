// --- START OF NEW FILE: TelegramPanel/Application/States/Admin/ForceJoinSetMessageState.cs ---
using Application.Interfaces;
using Telegram.Bot.Types;
using TelegramPanel.Application.Interfaces;
using static TelegramPanel.Infrastructure.ActualTelegramMessageActions;

namespace TelegramPanel.Application.States.Admin
{
    /// <summary>
    /// A state that waits for an admin to provide a new custom message for the Force Join feature.
    /// </summary>
    public class ForceJoinSetMessageState : ITelegramState
    {
        private readonly ITelegramMessageSender _messageSender;
        private readonly IAdminService _adminService;

        public string Name => "WaitingForForceJoinMessage";

        public ForceJoinSetMessageState(ITelegramMessageSender messageSender, IAdminService adminService)
        {
            _messageSender = messageSender;
            _adminService = adminService;
        }

        public Task<string?> GetEntryMessageAsync(long chatId, Update? triggerUpdate = null, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<string?>("Please send the new message for users who have not joined. Markdown is supported. Type /cancel to abort.");
        }

        public async Task<string?> ProcessUpdateAsync(Update update, CancellationToken cancellationToken = default)
        {
            if (update.Message?.Text == null) return Name;

            var chatId = update.Message.Chat.Id;
            var text = update.Message.Text.Trim();

            if (text.Equals("/cancel", StringComparison.OrdinalIgnoreCase))
            {
                await _messageSender.SendTextMessageAsync(chatId, "Operation cancelled.", cancellationToken: cancellationToken);
                return null; // Exit state
            }

            var settings = await _adminService.GetForceJoinSettingsAsync(cancellationToken);
            settings.Message = text;
            await _adminService.UpdateForceJoinSettingsAsync(settings, cancellationToken);

            await _messageSender.SendTextMessageAsync(chatId, "✅ Force Join message has been updated successfully.", cancellationToken: cancellationToken);

            // Return null to exit the state machine
            return null;
        }
    }
}