using Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace Application.DTOs
{
    #region SignalDto
    /// <summary>
    /// Data Transfer Object representing a complete trading signal.
    /// This DTO includes its core details, price levels, metadata, and related data like its category and analyses.
    /// </summary>
    public class SignalDto
    {
        #region Properties

        #region Core Signal Details
        /// <summary>
        /// Gets or sets the unique identifier for the signal. This is the primary key.
        /// </summary>
        /// <example>f47ac10b-58cc-4372-a567-0e02b2c3d479</example>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the type of the trading signal (e.g., Buy, Sell).
        /// </summary>
        /// <example>SignalType.Buy</example>
        public SignalType Type { get; set; }

        /// <summary>
        /// Gets or sets the financial instrument or trading symbol for the signal.
        /// </summary>
        /// <example>EURUSD</example>
        [Required(ErrorMessage = "The trading symbol is required.")]
        [StringLength(50, ErrorMessage = "The symbol cannot exceed 50 characters.")]
        public string Symbol { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the name of the provider or source that generated the signal.
        /// </summary>
        /// <example>ProTraderX</example>
        [Required(ErrorMessage = "The signal source is required.")]
        [StringLength(100, ErrorMessage = "The source name cannot exceed 100 characters.")]
        public string Source { get; set; } = string.Empty;
        #endregion

        #region Price Levels
        /// <summary>
        /// Gets or sets the recommended price at which to enter the trade.
        /// </summary>
        /// <example>1.08500</example>
        [Range(0.00000001, double.MaxValue, ErrorMessage = "The entry price must be a positive value.")]
        public decimal EntryPrice { get; set; }

        /// <summary>
        /// Gets or sets the price at which the trade should be closed to limit losses.
        /// </summary>
        /// <example>1.08000</example>
        [Range(0.00000001, double.MaxValue, ErrorMessage = "The stop-loss price must be a positive value.")]
        public decimal StopLoss { get; set; }

        /// <summary>
        /// Gets or sets the price at which the trade should be closed to lock in profits.
        /// </summary>
        /// <example>1.09500</example>
        [Range(0.00000001, double.MaxValue, ErrorMessage = "The take-profit price must be a positive value.")]
        public decimal TakeProfit { get; set; }
        #endregion

        #region Associated Data & Relationships
        /// <summary>
        /// Gets or sets the associated signal category information.
        /// This property will be null if the signal is not assigned to a category.
        /// </summary>
        /// <remarks>
        /// This property is populated via a navigation property or a join in the data retrieval query.
        /// </remarks>
        public SignalCategoryDto? Category { get; set; }

        /// <summary>
        /// Gets or sets a collection of analyses associated with this signal.
        /// This will be null or an empty collection if no analyses exist for this signal.
        /// </summary>
        /// <remarks>
        /// This property is typically lazy-loaded or explicitly included in the data retrieval query.
        /// </remarks>
        public IEnumerable<SignalAnalysisDto>? Analyses { get; set; }
        #endregion

        #region Timestamps
        /// <summary>
        /// Gets or sets the date and time when the signal was created in the system.
        /// </summary>
        /// <example>2024-05-21T10:00:00Z</example>
        public DateTime CreatedAt { get; set; }
        #endregion

        #endregion
    }
    #endregion
}