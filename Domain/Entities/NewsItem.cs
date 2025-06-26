// File: Domain/Entities/NewsItem.cs
#region Usings
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
#endregion

namespace Domain.Entities
{
    /// <summary>
    /// Represents a news item fetched from an RSS source or other news feeds.
    /// </summary>
    public class NewsItem
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        [MaxLength(500)]
        public string Title { get; set; } = string.Empty;

        [Required]
        [MaxLength(2083)]
        public string Link { get; set; } = string.Empty;

        public string? Summary { get; set; }
        public string? FullContent { get; set; }

        [MaxLength(2083)]
        public string? ImageUrl { get; set; }

        public DateTime PublishedDate { get; set; }

        [Required]
        public DateTime CreatedAt { get; set; }

        public DateTime? LastProcessedAt { get; set; }

        [MaxLength(150)]
        public string? SourceName { get; set; }

        [MaxLength(500)]
        public string? SourceItemId { get; set; }

        public double? SentimentScore { get; set; }

        [MaxLength(50)]
        public string? SentimentLabel { get; set; }

        [MaxLength(10)]
        public string? DetectedLanguage { get; set; }

        [MaxLength(500)]
        public string? AffectedAssets { get; set; }

        [Required]
        public Guid RssSourceId { get; set; }

        public bool IsVipOnly { get; set; }

        public Guid? AssociatedSignalCategoryId { get; set; }

        /// <summary>
        /// A SHA256 hash of the news item's link, used for fast, case-sensitive
        /// duplicate checking. Stored as a 32-byte binary array.
        /// </summary>
        public byte[]? LinkHash { get; set; }

        #region Navigation Properties
        [ForeignKey(nameof(RssSourceId))]
        public virtual RssSource RssSource { get; set; } = null!;

        [ForeignKey(nameof(AssociatedSignalCategoryId))]
        public virtual SignalCategory? AssociatedSignalCategory { get; set; }
        #endregion

        public NewsItem()
        {
            Id = Guid.NewGuid();
            CreatedAt = DateTime.UtcNow;
        }
    }
}