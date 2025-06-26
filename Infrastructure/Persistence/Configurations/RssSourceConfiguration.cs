// File: Infrastructure/Persistence/Configurations/RssSourceConfiguration.cs

#region Usings
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
#endregion

namespace Infrastructure.Persistence.Configurations
{
    /// <summary>
    /// Configures the database mapping for the RssSource entity using EF Core's Fluent API.
    /// This defines the table structure, primary key, properties, relationships, and performance-critical indexes.
    /// </summary>
    public class RssSourceConfiguration : IEntityTypeConfiguration<RssSource>
    {
        public void Configure(EntityTypeBuilder<RssSource> builder)
        {
            // --- Table and Primary Key ---
            builder.ToTable("RssSources");
            builder.HasKey(rs => rs.Id);
            builder.Property(rs => rs.Id).ValueGeneratedOnAdd();

            // --- Core Properties with Basic Indexes ---
            builder.Property(rs => rs.Url)
                .IsRequired()
                .HasMaxLength(2083);
            builder.HasIndex(rs => rs.Url)
                .IsUnique()
                .HasDatabaseName("IX_RssSources_Url"); // Give the index an explicit name.

            builder.Property(rs => rs.SourceName)
                .IsRequired()
                .HasMaxLength(150);
            builder.HasIndex(rs => rs.SourceName)
                .HasDatabaseName("IX_RssSources_SourceName"); // Good for admin UI searches.

            // --- Properties with Database-Generated Default Values ---
            builder.Property(rs => rs.IsActive)
                .IsRequired()
                .HasDefaultValue(true); // New sources should be active by default.

            builder.Property(rs => rs.CreatedAt)
                .IsRequired()
                .HasDefaultValueSql("NOW()"); // For PostgreSQL. Use GETUTCDATE() for SQL Server.

            builder.Property(rs => rs.FetchErrorCount)
                .IsRequired()
                .HasDefaultValue(0); // New sources start with zero errors.

            // --- Nullable & Other Properties ---
            builder.Property(rs => rs.UpdatedAt);
            builder.Property(rs => rs.LastModifiedHeader).HasMaxLength(100);
            builder.Property(rs => rs.ETag).HasMaxLength(255);
            builder.Property(rs => rs.LastFetchAttemptAt);
            builder.Property(rs => rs.LastSuccessfulFetchAt);
            builder.Property(rs => rs.FetchIntervalMinutes);
            builder.Property(rs => rs.Description).HasMaxLength(1000);
            builder.Property(rs => rs.DefaultSignalCategoryId);

            // --- RECOMMENDED PERFORMANCE INDEXES ---

            // 1. Index on IsActive for quickly finding all active sources.
            // This is the most common filter used by the RssFetchingCoordinatorService.
            builder.HasIndex(rs => rs.IsActive)
                   .HasDatabaseName("IX_RssSources_IsActive");

            // 2. Index for the scheduler to find the next source to fetch.
            // This simple index on LastFetchAttemptAt helps find sources that haven't been checked recently.
            builder.HasIndex(rs => rs.LastFetchAttemptAt)
                   .HasDatabaseName("IX_RssSources_LastFetchAttemptAt");

            // 3. OPTIMAL COMPOSITE INDEX for the scheduler's primary query pattern.
            // This index is highly effective for:
            // SELECT * FROM "RssSources" WHERE "IsActive" = true ORDER BY "LastFetchAttemptAt" ASC
            // It allows the database to filter by IsActive and use the sorted index for LastFetchAttemptAt
            // without needing an additional, costly sort operation on the entire filtered result set.
            builder.HasIndex(rs => new { rs.IsActive, rs.LastFetchAttemptAt })
                   .HasDatabaseName("IX_RssSources_IsActive_LastFetchAttemptAt");

            // --- Relationships ---

            // The one-to-many relationship with NewsItem is configured in NewsItemConfiguration.
            // EF Core is smart enough to link them, but explicitly defining it here ensures clarity.
            builder.HasMany(rs => rs.NewsItems)
                   .WithOne(ni => ni.RssSource)
                   .HasForeignKey(ni => ni.RssSourceId)
                   .OnDelete(DeleteBehavior.Cascade); // If an RssSource is deleted, delete all its NewsItems.

            // Optional relationship to SignalCategory for a default category.
            builder.HasOne(rs => rs.DefaultSignalCategory)
                   .WithMany() // Assuming SignalCategory doesn't have a navigation property back to RssSource.
                   .HasForeignKey(rs => rs.DefaultSignalCategoryId)
                   .OnDelete(DeleteBehavior.SetNull); // If the category is deleted, just set this FK to null.
        }
    }
}