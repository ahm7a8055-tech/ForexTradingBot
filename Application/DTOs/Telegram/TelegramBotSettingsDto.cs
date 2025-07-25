using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.Telegram
{
    #region TelegramBotSettingsDto
    /// <summary>
    /// Represents the configuration settings required to initialize and operate the Telegram bot.
    /// It encapsulates sensitive credentials, administrative access controls, and operational parameters.
    /// </summary>
    /// <remarks>
    /// These settings are typically bound from a configuration source (e.g., `appsettings.json`, environment variables, or a secure vault) during application startup.
    /// </remarks>
    public class TelegramBotSettingsDto
    {
        #region Properties

        #region Authentication
        /// <summary>
        /// Gets or sets the authentication token for the Telegram bot.
        /// This token is provided by BotFather and is required for all API interactions.
        /// </summary>
        /// <remarks>
        /// This is a highly sensitive secret and should be managed securely, never hardcoded in source control.
        /// </remarks>
        /// <example>1234567890:AAbbCCddEEffGGhhIIjjKKllMMnnOOppQQ</example>
        [Required(ErrorMessage = "The Bot Token is required.")]
        public string BotToken { get; set; } = string.Empty;
        #endregion

        #region Authorization & Access
        /// <summary>
        /// Gets or sets a list of unique Telegram user IDs that have administrative privileges.
        /// Users with these IDs can access restricted commands and features of the bot.
        /// </summary>
        /// <example>[123456789, 987654321]</example>
        public List<long> AdminUserIds { get; set; } = new();
        #endregion

        #region Operational Settings
        /// <summary>
        /// Gets or sets the optional chat ID of a private channel or user where the bot should send logs and notifications.
        /// </summary>
        /// <remarks>
        /// If this value is null, logging to Telegram is disabled. This is useful for monitoring bot activity, errors, and important events.
        /// Channel IDs are typically large negative numbers.
        /// </remarks>
        /// <example>-1001234567890</example>
        public long? ChatIdForLogs { get; set; }
        #endregion

        #endregion
    }
    #endregion
}