using System.ComponentModel.DataAnnotations;

namespace Application.DTOs
{
    #region SignalCategoryDto
    /// <summary>
    /// Data Transfer Object representing a signal category in the system.
    /// This DTO includes the category's core details and can optionally include calculated data
    /// like the number of associated signals.
    /// </summary>
    public class SignalCategoryDto
    {
        #region Properties

        #region Core Details
        /// <summary>
        /// Gets or sets the unique identifier for the signal category. This is the primary key.
        /// </summary>
        /// <example>f47ac10b-58cc-4372-a567-0e02b2c3d479</example>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the user-friendly name of the signal category.
        /// </summary>
        /// <example>Technology</example>
        [Required(ErrorMessage = "The category name is required.")]
        [StringLength(100, ErrorMessage = "The category name cannot exceed 100 characters.")]
        public string Name { get; set; } = string.Empty;
        #endregion

        #region Associated Data
        /// <summary>
        /// Gets or sets the total number of signals associated with this category.
        /// </summary>
        /// <remarks>
        /// This property is optional and not mapped directly to the database entity.
        /// It should be calculated on demand (e.g., via a specific query or projection)
        /// when returning lists of categories where the signal count is needed.
        /// It defaults to 0 if not calculated.
        /// </remarks>
        /// <example>42</example>
        public int SignalCount { get; set; }
        #endregion

        #endregion
    }
    #endregion
}