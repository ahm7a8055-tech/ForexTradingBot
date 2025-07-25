using System.ComponentModel.DataAnnotations;

namespace Application.DTOs
{
    #region RegisterUserDto
    /// <summary>
    /// Data transfer object for user registration.
    /// This class encapsulates the information needed to create a new user account,
    /// primarily focused on integration with a Telegram bot.
    /// </summary>
    public class RegisterUserDto
    {
        #region Properties

        #region Required Properties
        /// <summary>
        /// Gets or sets the user's chosen username. Must be between 3 and 100 characters.
        /// </summary>
        /// <example>johndoe123</example>
        [Required(ErrorMessage = "Username is required.")]
        [StringLength(100, MinimumLength = 3, ErrorMessage = "Username must be between 3 and 100 characters.")]
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the user's email address. Must be a valid email format and unique.
        /// </summary>
        /// <example>john.doe@example.com</example>
        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Invalid email format.")]
        [StringLength(200, ErrorMessage = "Email length cannot exceed 200 characters.")]
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the user's unique Telegram ID. This is essential for bot communication.
        /// </summary>
        /// <example>123456789</example>
        [Required(ErrorMessage = "Telegram ID is required.")]
        [StringLength(50, ErrorMessage = "Telegram ID length cannot exceed 50 characters.")]
        public string TelegramId { get; set; } = string.Empty;
        #endregion

        #region Optional Properties
        // /// <summary>
        // /// Gets or sets the user's password.
        // /// This property is intended for use with a potential web panel or alternative authentication method.
        // /// Uncomment and use when implementing password-based login.
        // /// </summary>
        // /// <example>S3cur3P@ssw0rd!</example>
        // [Required(ErrorMessage = "Password is required.")]
        // [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be at least 6 characters long.")]
        // public string Password { get; set; } = string.Empty;
        #endregion

        #endregion
    }
    #endregion
}