using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.Telegram
{
    #region TelegramClientSettingsDto
    /// <summary>
    /// Represents the configuration settings required to initialize a Telegram Client (User API).
    /// This is used for interacting with Telegram as a user account, not as a bot.
    /// </summary>
    /// <remarks>
    /// These credentials are obtained from my.telegram.org and grant extensive access to a user account.
    /// They are extremely sensitive and must be managed securely using a secret manager, environment variables, or other secure configuration sources.
    /// </remarks>
    public class TelegramClientSettingsDto
    {
        #region Properties

        #region Authentication Credentials
        /// <summary>
        /// Gets or sets the API ID obtained from the Telegram developer portal (my.telegram.org).
        /// </summary>
        /// <remarks>
        /// This is a secret credential and should never be hardcoded or checked into source control.
        /// </remarks>
        /// <example>1234567</example>
        [Required(ErrorMessage = "The API ID is required.")]
        public int ApiId { get; set; }

        /// <summary>
        /// Gets or sets the API Hash associated with the ApiId, obtained from the Telegram developer portal.
        /// </summary>
        /// <remarks>
        /// This is a secret credential and should never be hardcoded or checked into source control.
        /// </remarks>
        /// <example>a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6</example>
        [Required(ErrorMessage = "The API Hash is required.")]
        public string ApiHash { get; set; } = string.Empty;
        #endregion

        #endregion
    }
    #endregion
}