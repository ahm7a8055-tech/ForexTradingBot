using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.Telegram
{
    public class TelegramClientSettingsDto
    {
        [Required]
        public int ApiId { get; set; }

        [Required]
        public string ApiHash { get; set; } = string.Empty;
    }
}
