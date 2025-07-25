using Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace Application.DTOs
{
    #region CreateTransactionDto
    /// <summary>
    /// Data transfer object for creating a new financial transaction.
    /// This class captures the essential details of a financial event like a payment, deposit, or withdrawal.
    /// </summary>
    public class CreateTransactionDto
    {
        #region Properties

        #region Required Properties
        /// <summary>
        /// Gets or sets the unique identifier of the user initiating the transaction.
        /// </summary>
        /// <example>f47ac10b-58cc-4372-a567-0e02b2c3d479</example>
        [Required(ErrorMessage = "The user ID is required.")]
        public Guid UserId { get; set; }

        /// <summary>
        /// Gets or sets the monetary value of the transaction. Must be a positive number.
        /// </summary>
        /// <example>29.99</example>
        [Required(ErrorMessage = "The transaction amount is required.")]
        [Range(0.01, double.MaxValue, ErrorMessage = "The amount must be a positive value.")]
        public decimal Amount { get; set; }

        /// <summary>
        /// Gets or sets the type of the transaction.
        /// </summary>
        /// <example>TransactionType.Deposit</example>
        [Required(ErrorMessage = "The transaction type is required.")]
        public TransactionType Type { get; set; }
        #endregion

        #region Optional Properties
        /// <summary>
        /// Gets or sets an optional description for the transaction, useful for record-keeping or display to the user.
        /// </summary>
        /// <example>Payment for monthly premium subscription.</example>
        [StringLength(500, ErrorMessage = "The description cannot exceed 500 characters.")]
        public string? Description { get; set; }
        #endregion

        #endregion
    }
    #endregion
}