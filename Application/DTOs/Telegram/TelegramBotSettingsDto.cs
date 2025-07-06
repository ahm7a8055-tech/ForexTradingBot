using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.Telegram
{
    public class TelegramBotSettingsDto
    {
        [Required]
        public string BotToken { get; set; } = string.Empty;

        public List<long> AdminUserIds { get; set; } = new List<long>();

        public long? ChatIdForLogs { get; set; }
    }
}
