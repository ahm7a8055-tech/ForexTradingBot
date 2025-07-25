using Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace Application.DTOs
{
    #region TransactionDto
    /// <summary>
    /// Data Transfer Object representing a financial transaction.
    /// This DTO is used to transfer detailed transaction data between application layers.
    /// </summary>
    public class TransactionDto
    {
        #region Properties

        #region Identifiers
        /// <summary>
        /// Gets or sets the unique identifier for the transaction. This is the primary key.
        /// </summary>
        /// <example>a12b34c5-d678-e90f-1234-567890abcdef</example>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the unique identifier of the user associated with the transaction.
        /// </summary>
        /// <example>f47ac10b-58cc-4372-a567-0e02b2c3d479</example>
        public Guid UserId { get; set; }
        #endregion

        #region Transaction Details
        /// <summary>
        /// Gets or sets the monetary value of the transaction. Should be a positive value.
        /// </summary>
        /// <example>50.00</example>
        [Range(0.01, double.MaxValue, ErrorMessage = "The transaction amount must be a positive value.")]
        public decimal Amount { get; set; }

        /// <summary>
        /// Gets or sets the type of the transaction.
        /// </summary>
        /// <example>TransactionType.Deposit</example>
        public TransactionType Type { get; set; }

        /// <summary>
        /// Gets or sets the currency code for the transaction amount (e.g., ISO 4217 code).
        /// </summary>
        /// <example>USD</example>
        [StringLength(10, ErrorMessage = "The currency code cannot exceed 10 characters.")]
        public string? Currency { get; set; }

        /// <summary>
        /// Gets or sets an optional description of the transaction for display or record-keeping.
        /// </summary>
        /// <example>Monthly subscription payment</example>
        [StringLength(500, ErrorMessage = "The description cannot exceed 500 characters.")]
        public string? Description { get; set; }
        #endregion

        #region Timestamps
        /// <summary>
        /// Gets or sets the date and time when the transaction was officially recorded.
        /// </summary>
        /// <example>2024-05-21T10:15:00Z</example>
        public DateTime TransactionDate { get; set; }
        #endregion

        #endregion
    }
    #endregion
}