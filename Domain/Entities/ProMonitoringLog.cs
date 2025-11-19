#region Usings
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
#endregion

namespace Domain.Entities
{
    /// <summary>
    /// Represents a high-value, indexed log entry for pro-level monitoring, diagnostics, and job/event tracking.
    /// </summary>
    [Table("ProMonitoringLogs")]
    public class ProMonitoringLog
    {
        #region Core Properties
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        [Required]
        [MaxLength(20)]
        public string Level { get; set; } = "Info"; // Info, Warning, Error, Critical, Debug

        [MaxLength(100)]
        public string? Source { get; set; } // e.g., "Hangfire", "TelegramBot", "AIService"

        [MaxLength(50)]
        public string? EventType { get; set; } // e.g., "JobStarted", "JobFailed", "MessageSent"

        [MaxLength(100)]
        public string? JobId { get; set; } // Hangfire or external job ID

        [MaxLength(100)]
        public string? CorrelationId { get; set; } // For distributed tracing

        [MaxLength(100)]
        public string? UserId { get; set; } // Telegram/User/Operator ID

        [MaxLength(20)]
        public string? Status { get; set; } // e.g., "Success", "Failed", "Pending"
        #endregion

        #region Message & Details
        [Required]
        [MaxLength(500)]
        public string Message { get; set; } = string.Empty;

        [Column(TypeName = "TEXT")]
        public string? Details { get; set; } // Large details, stack trace, payload, etc.

        [Column(TypeName = "TEXT")]
        public string? Exception { get; set; } // Exception details if any

        [MaxLength(200)]
        public string? Tags { get; set; } // Comma-separated tags for fast filtering
        #endregion

        #region Auditing
        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }
        #endregion
    }
}