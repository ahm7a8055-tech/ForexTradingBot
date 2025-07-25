using System.ComponentModel.DataAnnotations;

namespace Application.DTOs
{
    #region SetUserPreferencesDto
    /// <summary>
    /// Data transfer object for setting a user's category preferences.
    /// This DTO is used to define the complete set of signal categories a user is subscribed to.
    /// </summary>
    /// <remarks>
    /// When this DTO is processed, it should **replace** all of the user's existing category preferences with the contents of the <see cref="CategoryIds"/> collection.
    /// </remarks>
    public class SetUserPreferencesDto
    {
        #region Properties

        #region Required Payload
        /// <summary>
        /// Gets or sets the unique identifier of the user whose preferences are being set.
        /// </summary>
        /// <remarks>
        /// While this ID is required in the DTO, in many API implementations (e.g., a secured endpoint for a logged-in user),
        /// this value should be taken from the authenticated user's claims rather than the request body to ensure security.
        /// </remarks>
        /// <example>f47ac10b-58cc-4372-a567-0e02b2c3d479</example>
        [Required(ErrorMessage = "The user ID is required.")]
        public Guid UserId { get; set; }

        /// <summary>
        /// Gets or sets the complete collection of signal category identifiers the user wishes to subscribe to.
        /// Providing an empty collection will clear all of the user's category preferences.
        /// </summary>
        /// <example>["f47ac10b-58cc-4372-a567-0e02b2c3d479", "a12b34c5-d678-e90f-1234-567890abcdef"]</example>
        [Required(ErrorMessage = "The category ID collection is required, even if it's empty.")]
        public IEnumerable<Guid> CategoryIds { get; set; } = [];
        #endregion

        #endregion
    }
    #endregion
}