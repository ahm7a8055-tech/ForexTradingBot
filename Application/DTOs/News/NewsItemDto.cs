// File: Application/DTOs/News/NewsItemDto.cs
#region Usings
using System.ComponentModel.DataAnnotations;
#endregion

namespace Application.DTOs.News
{
    #region NewsItemDto
    /// <summary>
    /// Data Transfer Object for representing a single news item.
    /// This DTO is used to transfer processed and enriched news data between application layers.
    /// </summary>
    public class NewsItemDto
    {
        #region Properties

        #region Core Content
        /// <summary>
        /// Gets or sets the unique identifier of the news item in the system.
        /// </summary>
        /// <example>a12b34c5-d678-e90f-1234-567890abcdef</example>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the title of the news item.
        /// </summary>
        /// <example>Global Markets Rally on Positive Economic Data</example>
        [Required(ErrorMessage = "The news title is required.")]
        [StringLength(255, ErrorMessage = "The title cannot exceed 255 characters.")]
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the direct URL link to the original news article.
        /// </summary>
        /// <example>https://www.news-source.com/articles/12345</example>
        [Required(ErrorMessage = "The article link is required.")]
        [Url(ErrorMessage = "The link must be a valid URL.")]
        [StringLength(500, ErrorMessage = "The link cannot exceed 500 characters.")]
        public string Link { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a short summary or description of the news item.
        /// </summary>
        /// <remarks>
        /// This content may contain HTML tags from the original source. The presentation layer is responsible for sanitizing or correctly rendering this content.
        /// </remarks>
        /// <example>A new report shows unexpected growth, leading to a surge in stock prices worldwide.</example>
        [StringLength(2000, ErrorMessage = "The summary cannot exceed 2000 characters.")]
        public string? Summary { get; set; }
        #endregion

        #region Image Handling
        /// <summary>
        /// Gets or sets the URL of the main image associated with the news item, if available from the source.
        /// </summary>
        /// <example>https://www.news-source.com/images/12345.jpg</example>
        [Url(ErrorMessage = "The image URL must be a valid URL.")]
        [StringLength(500, ErrorMessage = "The image URL cannot exceed 500 characters.")]
        public string? ImageUrl { get; set; }

        /// <summary>
        /// The fallback image URL used if no specific image is provided for the news item.
        /// </summary>
        private const string DefaultImageUrl = "https://i.postimg.cc/3RmJjBjY/Breaking-News.jpg";

        /// <summary>
        /// Gets the actual image URL that should be used for display.
        /// </summary>
        /// <remarks>
        /// This is a computed property that returns the specific <see cref="ImageUrl"/> if it exists,
        /// otherwise it returns the <see cref="DefaultImageUrl"/>.
        /// </remarks>
        public string ImageUrlOrDefault =>
            string.IsNullOrWhiteSpace(ImageUrl) ? DefaultImageUrl : ImageUrl;
        #endregion

        #region Source Information
        /// <summary>
        /// Gets or sets the original publication date and time of the news item from the RSS source, in UTC.
        /// </summary>
        /// <example>2024-05-22T14:30:00Z</example>
        public DateTime PublishedDate { get; set; }

        /// <summary>
        /// Gets or sets the name of the RSS source from which this news item was fetched.
        /// </summary>
        /// <example>Global News Network</example>
        [Required(ErrorMessage = "The source name is required.")]
        [StringLength(150, ErrorMessage = "The source name cannot exceed 150 characters.")]
        public string SourceName { get; set; } = string.Empty;
        #endregion

        #region Analysis & Metadata
        /// <summary>
        /// Gets or sets the sentiment score of the news item, populated if sentiment analysis is performed.
        /// The score typically ranges from -1.0 (very negative) to 1.0 (very positive).
        /// </summary>
        /// <example>0.75</example>
        public double? SentimentScore { get; set; }

        /// <summary>
        /// Gets or sets the human-readable label for the sentiment (e.g., "Positive", "Negative", "Neutral").
        /// </summary>
        /// <example>Positive</example>
        public string? SentimentLabel { get; set; }

        /// <summary>
        /// Gets or sets a list of assets or currencies potentially affected by this news.
        /// </summary>
        /// <remarks>This could be a comma-separated list or a serialized JSON string.</remarks>
        /// <example>EURUSD,SPX,AAPL</example>
        public string? AffectedAssets { get; set; }
        #endregion

        #region System Timestamps
        /// <summary>
        /// Gets or sets the date and time when this news item was added to our system, in UTC.
        /// </summary>
        /// <example>2024-05-22T14:35:10Z</example>
        public DateTime CreatedAtInSystem { get; set; }
        #endregion

        #endregion
    }
    #endregion
}