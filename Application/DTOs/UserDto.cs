using Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace Application.DTOs
{
    #region UserDto
    /// <summary>
    /// Data Transfer Object representing a comprehensive view of a user's information.
    /// This includes personal details, account status, and related data like their wallet and active subscription.
    /// </summary>
    public class UserDto
    {
        #region Properties

        #region User Identity & Contact
        /// <summary>
        /// Gets or sets the unique identifier of the user. This is the primary key.
        /// </summary>
        /// <example>f47ac10b-58cc-4372-a567-0e02b2c3d479</example>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the user's chosen username.
        /// </summary>
        /// <example>johndoe123</example>
        [Required]
        [StringLength(100)]
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the user's unique Telegram ID, used for bot communication.
        /// </summary>
        /// <example>123456789</example>
        [Required]
        [StringLength(50)]
        public string TelegramId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the user's email address.
        /// </summary>
        /// <example>john.doe@example.com</example>
        [Required]
        [EmailAddress]
        [StringLength(200)]
        public string Email { get; set; } = string.Empty;
        #endregion

        #region Account Status & Role
        /// <summary>
        /// Gets or sets the user's access level within the system.
        /// </summary>
        /// <example>UserLevel.Premium</example>
        public UserLevel Level { get; set; } = UserLevel.Free;
        #endregion

        #region Associated Data & Relationships
        /// <summary>
        /// Gets or sets the user's current token balance.
        /// </summary>
        /// <remarks>
        /// This is a convenience property, typically populated from the user's <see cref="TokenWallet"/>.
        /// </remarks>
        /// <example>250.50</example>
        public decimal TokenBalance { get; set; }

        /// <summary>
        /// Gets or sets the user's full token wallet details.
        /// This property may be null if not explicitly included in the data retrieval query.
        /// </summary>
        public TokenWalletDto? TokenWallet { get; set; }

        /// <summary>
        /// Gets or sets the user's currently active subscription details.
        /// This property will be null if the user has no active subscription.
        /// </summary>
        /// <remarks>
        /// This implies that business logic has been applied to find the specific subscription
        /// where the current date falls between its start and end dates.
        /// </remarks>
        public SubscriptionDto? ActiveSubscription { get; set; }
        #endregion

        #region Timestamps
        /// <summary>
        /// Gets or sets the date and time when the user account was created.
        /// </summary>
        /// <example>2024-01-01T10:00:00Z</example>
        public DateTime CreatedAt { get; set; }
        #endregion

        #endregion
    }
    #endregion
}