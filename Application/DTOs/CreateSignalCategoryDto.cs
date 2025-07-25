using System.ComponentModel.DataAnnotations;

namespace Application.DTOs
{
    #region CreateSignalCategoryDto
    /// <summary>
    /// Data transfer object for creating a new signal category.
    /// This is used to define a new category for organizing signals or news items.
    /// </summary>
    public class CreateSignalCategoryDto
    {
        #region Properties

        #region Required Properties
        /// <summary>
        /// Gets or sets the name of the signal category. This should be a unique, user-friendly name.
        /// </summary>
        /// <example>Technology</example>
        [Required(ErrorMessage = "The category name is required.")]
        [StringLength(100, ErrorMessage = "The category name cannot exceed 100 characters.")]
        public string Name { get; set; } = string.Empty;
        #endregion

        #endregion
    }
    #endregion
}