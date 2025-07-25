using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.Settings
{
    #region ForceJoinSettingsDto
    /// <summary>
    /// Represents the configuration for the 'Force Join' feature of a Telegram bot.
    /// This feature requires users to be a member of a specific Telegram channel before they can interact with the bot.
    /// </summary>
    /// <remarks>
    /// These settings are typically bound from a configuration source like `appsettings.json`.
    /// If <see cref="IsEnabled"/> is true, then all other properties must be configured correctly.
    /// </remarks>
    public class ForceJoinSettingsDto
    {
        #region Properties

        #region Activation
        /// <summary>
        /// Gets or sets a value indicating whether the Force Join feature is enabled.
        /// </summary>
        /// <remarks>
        /// If this is set to `false`, the bot will not perform any membership checks.
        /// </remarks>
        /// <example>true</example>
        public bool IsEnabled { get; set; }
        #endregion

        #region Channel Identification
        /// <summary>
        /// Gets or sets the unique numerical identifier of the required Telegram channel.
        /// </summary>
        /// <remarks>
        /// This ID is used by the bot's API to programmatically check if a user is a member.
        /// Public channel IDs are typically large negative numbers (e.g., -1001234567890).
        /// </remarks>
        /// <example>-1001234567890</example>
        [Required(ErrorMessage = "A Channel ID is required when Force Join is enabled.")]
        public long ChannelId { get; set; }

        /// <summary>
        /// Gets or sets the public invitation link for the channel (e.g., "https://t.me/mychannel").
        /// </summary>
        /// <remarks>
        /// This link is sent to users who are not yet members so they can join.
        /// </remarks>
        /// <example>https://t.me/my_awesome_channel</example>
        [Required(ErrorMessage = "A Channel Link is required when Force Join is enabled.")]
        [Url(ErrorMessage = "The Channel Link must be a valid URL.")]
        public string ChannelLink { get; set; } = string.Empty;
        #endregion

        #region User-Facing Content
        /// <summary>
        /// Gets or sets the message text sent to a user who has not joined the required channel.
        /// </summary>
        /// <remarks>
        /// This message should typically include the <see cref="ChannelLink"/> so the user can easily join.
        /// You can use a placeholder like {0} to format the link into the message.
        /// </remarks>
        /// <example>To use this bot, you must first join our channel: {0}. After joining, please type /start again.</example>
        [Required(ErrorMessage = "A message is required when Force Join is enabled.")]
        public string Message { get; set; } = string.Empty;
        #endregion

        #endregion
    }
    #endregion
}