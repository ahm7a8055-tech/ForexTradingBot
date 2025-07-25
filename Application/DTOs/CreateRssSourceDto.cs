using System.ComponentModel.DataAnnotations;

namespace Application.DTOs
{
    #region CreateRssSourceDto
    /// <summary>
    /// Data transfer object for creating a new RSS source.
    /// This class encapsulates all the necessary information required to register and configure a new feed source in the system.
    /// </summary>
    public class CreateRssSourceDto
    {
        #region Properties

        #region Required Properties
        /// <summary>
        /// Gets or sets the URL of the RSS feed. This must be a valid and accessible URL.
        /// </summary>
        /// <example>https://www.my-news-site.com/rss</example>
        [Required(ErrorMessage = "The RSS feed URL is required.")]
        [Url(ErrorMessage = "The URL provided is not a valid URL.")]
        [StringLength(500, ErrorMessage = "The URL cannot exceed 500 characters.")]
        public string Url { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the user-defined name for the RSS source. This is used for display purposes.
        /// </summary>
        /// <example>My Favorite News Site</example>
        [Required(ErrorMessage = "The source name is required.")]
        [StringLength(150, ErrorMessage = "The source name cannot exceed 150 characters.")]
        public string SourceName { get; set; } = string.Empty;
        #endregion

        #region Optional Properties
        /// <summary>
        /// Gets or sets a value indicating whether the RSS source is active.
        /// If true, the source will be fetched for new items. Defaults to true.
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Gets or sets an optional, user-defined description for the RSS source for easier identification.
        /// </summary>
        /// <example>This source provides the latest tech news.</example>
        public string? Description { get; set; }

        /// <summary>
        /// Gets or sets an optional override for the fetch interval in minutes.
        /// If not set, a system-wide default will be used.
        /// </summary>
        /// <example>60</example>
        public int? FetchIntervalMinutes { get; set; }

        /// <summary>
        /// Gets or sets an optional default signal category ID.
        /// All new signals created from this source will be assigned to this category if specified.
        /// </summary>
        public Guid? DefaultSignalCategoryId { get; set; }

        /// <summary>
        /// Gets or sets the ETag (Entity Tag) for the RSS feed.
        /// This is used in 'If-None-Match' HTTP headers to perform conditional GET requests,
        /// reducing bandwidth usage if the feed has not changed. This is typically set by the system on the first fetch.
        /// </summary>
        public string? ETag { get; set; }
        #endregion

        #endregion
    }
    #endregion
}