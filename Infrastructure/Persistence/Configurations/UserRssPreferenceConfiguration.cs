// File: Infrastructure/Persistence/Configurations/UserRssPreferenceConfiguration.cs
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations
{
    /// <summary>
    /// Configures the 'UserRssPreference' join table, which links Users to RssSources.
    /// </summary>
    public class UserRssPreferenceConfiguration : IEntityTypeConfiguration<UserRssPreference>
    {
        public void Configure(EntityTypeBuilder<UserRssPreference> builder)
        {
            _ = builder.ToTable("UserRssPreferences");

            // Define the composite primary key to ensure a user can only subscribe once to a source.
            _ = builder.HasKey(p => new { p.UserId, p.RssSourceId });

            // --- HIGH-PERFORMANCE INDEX FOR NOTIFICATION DISPATCH ---
            // Purpose: Massively speeds up finding all users subscribed to a specific RSS source.
            // Query Optimized: `SELECT UserId FROM UserRssPreferences WHERE RssSourceId = @p0`
            // Why: When a new article is published, this index allows the system to instantly
            // get the list of users to notify, which is the most critical query for this table.
            _ = builder.HasIndex(p => p.RssSourceId)
                   .HasDatabaseName("IX_UserRssPreferences_BySource");

            // --- Relationships ---
            _ = builder.HasOne(p => p.User)
                   .WithMany(u => u.RssPreferences)
                   .HasForeignKey(p => p.UserId)
                   .OnDelete(DeleteBehavior.Cascade); // If a user is deleted, their preferences are deleted.

            _ = builder.HasOne(p => p.RssSource)
                   .WithMany(s => s.UserPreferences)
                   .HasForeignKey(p => p.RssSourceId)
                   .OnDelete(DeleteBehavior.Cascade); // If a source is deleted, all subscriptions to it are deleted.
        }
    }
}