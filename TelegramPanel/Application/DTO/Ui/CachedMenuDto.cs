// --- START OF NEW FILE: CachedMenuDto.cs ---

using Telegram.Bot.Types.ReplyMarkups;

namespace TelegramPanel.Application.DTOs.Ui
{
    /// <summary>
    /// A Data Transfer Object used to cache a pre-generated Telegram menu,
    /// including its text and keyboard markup. This is a reference type,
    /// making it compatible with generic cache services.
    /// </summary>
    public class CachedMenuDto
    {
        public string Text { get; }
        public InlineKeyboardMarkup Keyboard { get; }

        public CachedMenuDto(string text, InlineKeyboardMarkup keyboard)
        {
            Text = text;
            Keyboard = keyboard;
        }
    }
}