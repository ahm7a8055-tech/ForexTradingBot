using System.ComponentModel.DataAnnotations;

namespace Application.DTOs
{
    #region RssSourceDto
    /// <summary>
    /// Data Transfer Object representing a single RSS source.
    /// This DTO contains all the details of an RSS feed, including its configuration, status, and system-managed metadata.
    /// </summary>
    public class RssSourceDto
    {
        #region Properties

        #region Core Identifiers
        /// <summary>
        /// Gets or sets the unique identifier of the RSS source.
        /// </summary>
        /// <example>a12b34c5-d678-e90f-1234-567890abcdef</example>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the URL of the RSS feed.
        /// </summary>
        /// <example>https://www.my-news-site.com/rss</example>
        [Required(ErrorMessage = "The RSS feed URL is required.")]
        [Url(ErrorMessage = "The URL provided is not a valid URL.")]
        [StringLength(500, ErrorMessage = "The URL cannot exceed 500 characters.")]
        public string Url { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the user-defined name for the RSS source.
        /// </summary>
        /// <example>My Favorite News Site</example>
        [Required(ErrorMessage = "The source name is required.")]
        [StringLength(150, ErrorMessage = "The source name cannot exceed 150 characters.")]
        public string SourceName { get; set; } = string.Empty;
        #endregion

        #region Configuration & Status
        /// <summary>
        /// Gets or sets a value indicating whether the RSS source is active and should be fetched.
        /// </summary>
        public bool IsActive { get; set; }

        /// <summary>
        /// Gets or sets an optional description for the RSS source.
        /// </summary>
        /// <example>This source provides the latest tech news.</example>
        [StringLength(1000, ErrorMessage = "The description cannot exceed 1000 characters.")]
        public string? Description { get; set; }

        /// <summary>
        /// Gets or sets the configured fetch interval in minutes for this source. A null value indicates that a system-wide default is used.
        /// </summary>
        /// <example>60</example>
        public int? FetchIntervalMinutes { get; set; }

        /// <summary>
        /// Gets or sets the unique identifier of the default signal category for signals generated from this RSS source.
        /// </summary>
        /// <example>f47ac10b-58cc-4372-a567-0e02b2c3d479</example>
        public Guid? DefaultSignalCategoryId { get; set; }

        /// <summary>
        /// Gets or sets the name of the default signal category. This is included for convenience and display purposes, avoiding an extra lookup.
        /// </summary>
        /// <example>Technology News</example>
        public string? DefaultSignalCategoryName { get; set; }
        #endregion

        #region System & State Information
        /// <summary>
        /// Gets or sets the date and time when the RSS source was created in the system.
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Gets or sets the date and time when the RSS source was last updated.
        /// </summary>
        public DateTime? UpdatedAt { get; set; }

        /// <summary>
        /// Gets or sets the date and time when the system last attempted to fetch this RSS source.
        /// </summary>
        public DateTime? LastFetchedAt { get; set; }

        /// <summary>
        /// Gets or sets a counter for consecutive errors encountered during the fetching process. This can be used to temporarily disable a faulty source.
        /// </summary>
        public int FetchErrorCount { get; set; }

        /// <summary>
        /// Gets or sets the ETag (Entity Tag) provided by the server for the RSS feed.
        /// This is used for caching to prevent re-downloading unchanged content.
        /// </summary>
        [StringLength(255, ErrorMessage = "The ETag cannot exceed 255 characters.")]
        public string? ETag { get; set; }
        #endregion

        #endregion
    }
    #endregion
}