using System.Collections.Generic;

namespace Application.Interfaces
{
    /// <summary>
    /// Defines a service for generating unique and relevant sets of hashtags for channel posts.
    /// </summary>
    public interface IHashtagService
    {
        /// <summary>
        /// Gets a unique, randomly selected set of hashtags suitable for a channel post.
        /// This method ensures that hashtag combinations don't repeat too frequently.
        /// </summary>
        /// <param name="content">The text content (title + summary) of the news item to analyze.</param>
        /// <param name="limit">The maximum number of hashtags to return.</param>
        /// <returns>A formatted string of hashtags (e.g., "#Forex #BTC #Trading").</returns>
        string GetRandomHashtags(int count = 3);
    }
}