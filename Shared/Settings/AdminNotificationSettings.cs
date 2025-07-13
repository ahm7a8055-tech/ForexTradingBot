namespace Shared.Settings
{
    /// <summary>
    /// Holds configuration settings for sending notifications to the administrator.
    /// </summary>
    public class AdminNotificationSettings
    {
        public const string SectionName = "AdminNotificationSettings";

        /// <summary>
        /// The unique Telegram Chat ID of the administrator who will receive notifications.
        /// </summary>
        public long AdminChatId { get; set; }
    }
}