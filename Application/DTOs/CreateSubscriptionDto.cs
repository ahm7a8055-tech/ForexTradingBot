using System.ComponentModel.DataAnnotations;

namespace Application.DTOs
{
    #region CreateSubscriptionDto
    /// <summary>
    /// Data Transfer Object for creating a new user subscription.
    /// This class specifies the user and the validity period for the new subscription.
    /// </summary>
    public class CreateSubscriptionDto
    {
        #region Properties

        #region Required Properties
        /// <summary>
        /// Gets or sets the unique identifier of the user who will own this subscription.
        /// </summary>
        /// <example>f47ac10b-58cc-4372-a567-0e02b2c3d479</example>
        [Required(ErrorMessage = "The user ID is required.")]
        public Guid UserId { get; set; }

        /// <summary>
        /// Gets or sets the date and time when the subscription becomes active.
        /// </summary>
        /// <example>2024-01-01T00:00:00Z</example>
        [Required(ErrorMessage = "The subscription start date is required.")]
        public DateTime StartDate { get; set; }

        /// <summary>
        /// Gets or sets the date and time when the subscription expires. This should be later than the StartDate.
        /// </summary>
        /// <example>2025-01-01T00:00:00Z</example>
        [Required(ErrorMessage = "The subscription end date is required.")]
        public DateTime EndDate { get; set; }
        #endregion

        #region Optional Properties
        // /// <summary>
        // /// Gets or sets the unique identifier for a specific subscription plan (e.g., 'Basic', 'Premium').
        // /// Uncomment this property if your system uses different subscription tiers or plans.
        // /// </summary>
        // /// <example>a12b34c5-d678-e90f-1234-567890abcdef</example>
        // [Required(ErrorMessage = "The plan ID is required if plans are used.")]
        // public Guid PlanId { get; set; }

        #endregion

        #endregion
    }
    #endregion
}