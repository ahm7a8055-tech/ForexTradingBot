using System.ComponentModel.DataAnnotations;

namespace Application.DTOs
{
    #region SubscriptionDto
    /// <summary>
    /// Data Transfer Object representing a user's subscription information.
    /// This DTO contains the validity period, status, and related metadata of a user's subscription.
    /// </summary>
    public class SubscriptionDto
    {
        #region Properties

        #region Identifiers
        /// <summary>
        /// Gets or sets the unique identifier for the subscription record.
        /// </summary>
        /// <example>a12b34c5-d678-e90f-1234-567890abcdef</example>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the unique identifier of the user who owns this subscription.
        /// </summary>
        /// <example>f47ac10b-58cc-4372-a567-0e02b2c3d479</example>
        public Guid UserId { get; set; }
        #endregion

        #region Subscription Period & Status
        /// <summary>
        /// Gets or sets the date and time when the subscription becomes active.
        /// </summary>
        /// <example>2024-01-01T00:00:00Z</example>
        public DateTime StartDate { get; set; }

        /// <summary>
        /// Gets or sets the date and time when the subscription expires.
        /// </summary>
        /// <example>2025-01-01T00:00:00Z</example>
        public DateTime EndDate { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the subscription is currently active.
        /// </summary>
        /// <remarks>
        /// This is a calculated property and is not stored in the database.
        /// It should be computed in the business/application layer based on the current date
        /// relative to the <see cref="StartDate"/> and <see cref="EndDate"/>.
        /// </remarks>
        /// <example>true</example>
        public bool IsActive { get; set; }
        #endregion

        #region Associated Data
        /// <summary>
        /// Gets or sets the name of the subscription plan (e.g., "Basic", "Premium").
        /// </summary>
        /// <remarks>
        /// This property is optional and should be populated from a related Subscription Plan entity if one exists.
        /// It can be null if the subscription is not linked to a named plan.
        /// </remarks>
        /// <example>Premium</example>
        public string? PlanName { get; set; }
        #endregion

        #region Timestamps
        /// <summary>
        /// Gets or sets the date and time when the subscription record was created in the system.
        /// </summary>
        /// <example>2024-01-01T00:00:00Z</example>
        public DateTime CreatedAt { get; set; }
        #endregion

        #endregion
    }
    #endregion
}