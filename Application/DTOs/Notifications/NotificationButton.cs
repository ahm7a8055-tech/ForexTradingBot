// File: Application/DTOs/Notifications/NotificationButton.cs
#region Usings
using System.ComponentModel.DataAnnotations;
#endregion

namespace Application.DTOs.Notifications
{
    #region NotificationButton
    /// <summary>
    /// Represents a single interactive button to be included in a notification message, such as for a Telegram inline keyboard.
    /// </summary>
    /// <remarks>
    /// This DTO is designed to be flexible enough to create either a button that triggers a callback query
    /// or a button that opens a URL.
    /// </remarks>
    public class NotificationButton
    {
        #region Properties

        #region Content
        /// <summary>
        /// Gets or sets the text displayed on the button.
        /// </summary>
        /// <example>View Details</example>
        [Required(ErrorMessage = "Button text is required.")]
        [StringLength(64, ErrorMessage = "Button text cannot exceed 64 characters.")]
        public string Text { get; set; } = string.Empty;
        #endregion

        #region Action
        /// <summary>
        /// Gets or sets the data payload for the button's action.
        /// This will be either a callback data string or a fully qualified URL.
        /// </summary>
        /// <remarks>
        /// The meaning of this property is determined by the <see cref="IsUrl"/> flag.
        /// - If `IsUrl` is `false`, this should be a callback data string (e.g., "command_param1").
        /// - If `IsUrl` is `true`, this must be a valid URL (e.g., "https://example.com").
        /// Note: Telegram has a 1-64 byte limit for callback data.
        /// </remarks>
        /// <example>signal_details_f47ac10b</example>
        [Required(ErrorMessage = "Callback data or a URL is required for the button's action.")]
        [StringLength(256, ErrorMessage = "Callback data or URL cannot exceed 256 characters.")]
        public string CallbackDataOrUrl { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether the <see cref="CallbackDataOrUrl"/> property should be treated as a URL.
        /// </summary>
        /// <remarks>
        /// - Set to `false` (default) for a standard inline button that sends a callback query to the bot.
        /// - Set to `true` for a button that opens the specified link in the user's browser.
        /// </remarks>
        /// <example>false</example>
        public bool IsUrl { get; set; }
        #endregion

        #endregion
    }
    #endregion
}