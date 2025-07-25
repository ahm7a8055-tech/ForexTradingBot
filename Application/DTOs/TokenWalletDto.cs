using System.ComponentModel.DataAnnotations;

namespace Application.DTOs
{
    #region TokenWalletDto
    /// <summary>
    /// Data Transfer Object representing a user's token wallet.
    /// This DTO contains the wallet's balance, status, and system-managed metadata.
    /// </summary>
    public class TokenWalletDto
    {
        #region Properties

        #region Identifiers
        /// <summary>
        /// Gets or sets the unique identifier for the token wallet. This is the primary key.
        /// </summary>
        /// <example>a12b34c5-d678-e90f-1234-567890abcdef</example>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the unique identifier of the user who owns this wallet.
        /// </summary>
        /// <example>f47ac10b-58cc-4372-a567-0e02b2c3d479</example>
        [Required(ErrorMessage = "The user ID is required.")]
        public Guid UserId { get; set; }
        #endregion

        #region Wallet State
        /// <summary>
        /// Gets or sets the current balance of tokens or credits in the wallet. This value cannot be negative.
        /// </summary>
        /// <example>150.75</example>
        [Range(0, double.MaxValue, ErrorMessage = "The balance cannot be negative.")]
        public decimal Balance { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the wallet is active and can be used for transactions.
        /// </summary>
        /// <remarks>
        /// A wallet might be deactivated for security reasons or if the user's account is suspended.
        /// </remarks>
        /// <example>true</example>
        public bool IsActive { get; set; }
        #endregion

        #region Timestamps
        /// <summary>
        /// Gets or sets the date and time when the wallet was first created.
        /// </summary>
        /// <example>2024-01-01T10:00:00Z</example>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Gets or sets the date and time when the wallet was last updated (e.g., when the balance changed).
        /// </summary>
        /// <example>2024-05-21T15:30:00Z</example>
        public DateTime UpdatedAt { get; set; }
        #endregion

        #endregion
    }
    #endregion
}