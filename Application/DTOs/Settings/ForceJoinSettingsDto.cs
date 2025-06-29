namespace Application.DTOs.Settings
{
    public class ForceJoinSettingsDto
    {
        public bool IsEnabled { get; set; }
        public long ChannelId { get; set; }

        public string ChannelLink { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;
    }
}