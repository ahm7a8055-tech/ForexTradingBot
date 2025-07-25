using System.ComponentModel.DataAnnotations;

namespace Application.DTOs
{
    #region UserSignalPreferenceDto
    /// <summary>
    /// Data Transfer Object representing a user's preference for a single signal category.
    /// This DTO is typically used within a collection to display all categories a user is subscribed to.
    /// </summary>
    public class UserSignalPreferenceDto
    {
        #region Properties

        #region Preference Details
        /// <summary>
        /// Gets or sets the unique identifier of the signal category the user is subscribed to.
        /// </summary>
        /// <example>f47ac10b-58cc-4372-a567-0e02b2c3d479</example>
        [Required(ErrorMessage = "The category ID is required.")]
        public Guid CategoryId { get; set; }

        /// <summary>
        /// Gets or sets the name of the signal category.
        /// This is included for display convenience, avoiding the need for an additional data lookup.
        /// </summary>
        /// <example>Technology News</example>
        [Required(ErrorMessage = "The category name is required.")]
        [StringLength(100, ErrorMessage = "The category name cannot exceed 100 characters.")]
        public string CategoryName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the date and time when the user subscribed to this signal category.
        /// </summary>
        /// <remarks>
        /// This corresponds to the 'CreatedAt' timestamp of the underlying UserSignalPreference join entity.
        /// </remarks>
        /// <example>2024-03-15T11:45:00Z</example>
        public DateTime SubscribedAt { get; set; }
        #endregion

        #endregion
    }
    #endregion
}