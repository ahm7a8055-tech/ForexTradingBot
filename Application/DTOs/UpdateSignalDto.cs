using Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace Application.DTOs
{
    #region UpdateSignalDto
    /// <summary>
    /// Data Transfer Object for updating an existing trading signal.
    /// </summary>
    /// <remarks>
    /// All properties are nullable to support partial updates (i.e., using an HTTP PATCH method).
    /// When processing this DTO, the application logic should only update the fields that are not null.
    /// </remarks>
    public class UpdateSignalDto
    {
        #region Properties

        #region Core Signal Details
        /// <summary>
        /// Gets or sets the type of the trading signal (e.g., Buy, Sell).
        /// </summary>
        /// <example>SignalType.Sell</example>
        public SignalType? Type { get; set; }

        /// <summary>
        /// Gets or sets the financial instrument or trading symbol for the signal.
        /// </summary>
        /// <example>BTCUSD</example>
        [StringLength(50, ErrorMessage = "The symbol cannot exceed 50 characters.")]
        public string? Symbol { get; set; }

        /// <summary>
        /// Gets or sets the name of the provider or source that generated the signal.
        /// </summary>
        /// <example>Advanced Signal Provider</example>
        [StringLength(100, ErrorMessage = "The source name cannot exceed 100 characters.")]
        public string? Source { get; set; }
        #endregion

        #region Price Levels
        /// <summary>
        /// Gets or sets the recommended price at which to enter the trade. Must be a positive value if provided.
        /// </summary>
        /// <example>65500.00</example>
        [Range(0.00000001, double.MaxValue, ErrorMessage = "If provided, the entry price must be a positive value.")]
        public decimal? EntryPrice { get; set; }

        /// <summary>
        /// Gets or sets the price at which the trade should be closed to limit losses. Must be a positive value if provided.
        /// </summary>
        /// <example>64000.50</example>
        [Range(0.00000001, double.MaxValue, ErrorMessage = "If provided, the stop-loss price must be a positive value.")]
        public decimal? StopLoss { get; set; }

        /// <summary>
        /// Gets or sets the price at which the trade should be closed to lock in profits. Must be a positive value if provided.
        /// </summary>
        /// <example>68000.00</example>
        [Range(0.00000001, double.MaxValue, ErrorMessage = "If provided, the take-profit price must be a positive value.")]
        public decimal? TakeProfit { get; set; }
        #endregion

        #region Relationships
        /// <summary>
        /// Gets or sets the unique identifier of the signal category this signal should be associated with.
        /// </summary>
        /// <example>f47ac10b-58cc-4372-a567-0e02b2c3d479</example>
        public Guid? CategoryId { get; set; }
        #endregion

        #endregion
    }
    #endregion
}