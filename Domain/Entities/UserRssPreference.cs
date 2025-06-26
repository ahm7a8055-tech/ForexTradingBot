// File: Domain/Entities/UserRssPreference.cs
namespace Domain.Entities
{
    /// <summary>
    /// Represents the many-to-many relationship between a User and an RssSource,
    /// indicating that a user has subscribed to receive news from a specific source.
    /// </summary>
    public class UserRssPreference
    {
        public Guid UserId { get; set; }
        public virtual User User { get; set; } = null!;

        public Guid RssSourceId { get; set; }
        public virtual RssSource RssSource { get; set; } = null!;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}