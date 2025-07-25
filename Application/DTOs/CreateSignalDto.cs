using Domain.Enums; // For SignalType
using System.ComponentModel.DataAnnotations;

namespace Application.DTOs
{
    #region CreateSignalDto
    /// <summary>
    /// Data Transfer Object for creating a new trading signal.
    /// This class encapsulates all the necessary details to define a trade opportunity.
    /// </summary>
    public class CreateSignalDto
    {
        #region Properties

        #region Required Properties
        /// <summary>
        /// Gets or sets the type of the trading signal, such as Buy or Sell.
        /// </summary>
        /// <example>SignalType.Buy</example>
        [Required(ErrorMessage = "The signal type is required.")]
        public SignalType Type { get; set; }

        /// <summary>
        /// Gets or sets the financial instrument or trading symbol for the signal.
        /// </summary>
        /// <example>EURUSD</example>
        [Required(ErrorMessage = "The trading symbol is required.")]
        [StringLength(50, ErrorMessage = "The symbol cannot exceed 50 characters.")]
        public string Symbol { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the recommended price at which to enter the trade. Must be a positive value.
        /// </summary>
        /// <example>1.08500</example>
        [Required(ErrorMessage = "The entry price is required.")]
        [Range(0.00000001, double.MaxValue, ErrorMessage = "The entry price must be a positive value.")]
        public decimal EntryPrice { get; set; }

        /// <summary>
        /// Gets or sets the price at which the trade should be closed to limit losses. Must be a positive value.
        /// </summary>
        /// <example>1.08000</example>
        [Required(ErrorMessage = "The stop-loss price is required.")]
        [Range(0.00000001, double.MaxValue, ErrorMessage = "The stop-loss price must be a positive value.")]
        public decimal StopLoss { get; set; }

        /// <summary>
        /// Gets or sets the price at which the trade should be closed to lock in profits. Must be a positive value.
        /// </summary>
        /// <example>1.09500</example>
        [Required(ErrorMessage = "The take-profit price is required.")]
        [Range(0.00000001, double.MaxValue, ErrorMessage = "The take-profit price must be a positive value.")]
        public decimal TakeProfit { get; set; }

        /// <summary>
        /// Gets or sets the name of the provider or source that generated the signal.
        /// </summary>
        /// <example>ProTraderX</example>
        [Required(ErrorMessage = "The signal source is required.")]
        [StringLength(100, ErrorMessage = "The source name cannot exceed 100 characters.")]
        public string Source { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the unique identifier (GUID) of the signal category this signal is associated with.
        /// </summary>
        /// <example>f47ac10b-58cc-4372-a567-0e02b2c3d479</example>
        [Required(ErrorMessage = "The category ID is required.")]
        public Guid CategoryId { get; set; }
        #endregion

        #endregion
    }
    #endregion
}