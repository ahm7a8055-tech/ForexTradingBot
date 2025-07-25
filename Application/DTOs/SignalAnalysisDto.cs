using System.ComponentModel.DataAnnotations;

namespace Application.DTOs
{
    #region SignalAnalysisDto
    /// <summary>
    /// Data Transfer Object (DTO) representing the analysis of a trading signal.
    /// This class is used to transfer detailed analysis data, such as expert commentary or automated checks,
    /// associated with a specific signal.
    /// </summary>
    public class SignalAnalysisDto
    {
        #region Properties

        #region Identifiers
        /// <summary>
        /// Gets or sets the unique identifier for this specific signal analysis entry.
        /// </summary>
        /// <example>a12b34c5-d678-e90f-1234-567890abcdef</example>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the unique identifier of the trading signal that this analysis pertains to.
        /// </summary>
        /// <example>f47ac10b-58cc-4372-a567-0e02b2c3d479</example>
        public Guid SignalId { get; set; }
        #endregion

        #region Analysis Details
        /// <summary>
        /// Gets or sets the name of the analyst or automated system that performed the analysis.
        /// </summary>
        /// <example>John Analyst</example>
        [Required(ErrorMessage = "The analyst name is required.")]
        [StringLength(150, ErrorMessage = "The analyst name cannot exceed 150 characters.")]
        public string AnalystName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the detailed analysis notes, comments, or observations.
        /// This can include technical, fundamental, or contextual insights.
        /// </summary>
        /// <example>Strong bullish divergence on the H4 RSI, confluence with major support at 1.08000.</example>
        [Required(ErrorMessage = "Analysis notes are required.")]
        [StringLength(2000, ErrorMessage = "The notes cannot exceed 2000 characters.")]
        public string Notes { get; set; } = string.Empty;
        #endregion

        #region Timestamps
        /// <summary>
        /// Gets or sets the date and time when the analysis was created.
        /// </summary>
        /// <example>2024-05-21T14:30:00Z</example>
        public DateTime CreatedAt { get; set; }
        #endregion

        #endregion
    }
    #endregion
}