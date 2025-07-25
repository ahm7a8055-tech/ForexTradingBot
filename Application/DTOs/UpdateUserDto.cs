using System.ComponentModel.DataAnnotations;

namespace Application.DTOs
{
    #region UpdateUserDto
    /// <summary>
    /// Data Transfer Object for updating an existing user's profile information.
    /// </summary>
    /// <remarks>
    /// All properties are nullable to support partial updates (e.g., via an HTTP PATCH request).
    /// The application logic should only update the fields that are provided (not null).
    /// The user's unique identifier (Id) should be obtained from the URL route or the authenticated user's context, not from this DTO's body.
    /// </remarks>
    public class UpdateUserDto
    {
        #region Properties

        #region User-Modifiable Details
        /// <summary>
        /// Gets or sets the user's new username. Must be between 3 and 100 characters.
        /// </summary>
        /// <example>johndoe_new</example>
        [StringLength(100, MinimumLength = 3, ErrorMessage = "Username must be between 3 and 100 characters.")]
        public string? Username { get; set; }

        /// <summary>
        /// Gets or sets the user's new email address. Must be a valid email format.
        /// </summary>
        /// <example>john.doe.new@example.com</example>
        [EmailAddress(ErrorMessage = "Invalid email format.")]
        [StringLength(200, ErrorMessage = "Email cannot exceed 200 characters.")]
        public string? Email { get; set; }
        #endregion

        #region Security Details
        // /// <summary>
        // /// Gets or sets a new password for the user.
        // /// This should be handled in a separate, dedicated endpoint (e.g., /api/users/change-password) for better security.
        // /// </summary>
        // /// <example>NewS3cur3P@ssw0rd!</example>
        // [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be at least 6 characters long.")]
        // public string? Password { get; set; }
        #endregion

        #region Omitted Properties (For Clarity)
        // --- The following properties are intentionally omitted from this DTO ---
        //
        // Guid Id: The user ID is specified in the request endpoint (e.g., api/users/{id}) or derived from the authentication token.
        //
        // string TelegramId: This is typically an immutable identifier assigned at registration and should not be changed by the user.
        //
        // UserLevel/Role: Modifying a user's role or access level is a privileged, administrative action and should have its own dedicated DTO and secured endpoint.
        #endregion

        #endregion
    }
    #endregion
}