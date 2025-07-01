// File: Infrastructure/Persistence/Configurations/NewsItemConfiguration.cs

#region Usings
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
#endregion

namespace Infrastructure.Persistence.Configurations
{
    /// <summary>
    /// Provides a complete and optimized database mapping for the NewsItem entity.
    /// This configuration is the single source of truth for the NewsItems table schema,
    /// including properties, column types, default values, relationships, and a comprehensive
    /// set of high-performance indexes tailored for common query patterns.
    /// </summary>
    public class NewsItemConfiguration : IEntityTypeConfiguration<NewsItem>
    {
        public void Configure(EntityTypeBuilder<NewsItem> builder)
        {
            _ = builder.ToTable("NewsItems");
            _ = builder.HasKey(ni => ni.Id);

            // --- Explicit Column Type Mapping & Properties ---
            // We define types here to ensure consistency and portability.
            // Note: C# 'Guid' maps to PostgreSQL 'uuid' by default.
            // Note: C# 'DateTime' maps to PostgreSQL 'timestamp with time zone' by default.

            _ = builder.Property(ni => ni.Title).IsRequired().HasMaxLength(500);
            _ = builder.Property(ni => ni.Link).IsRequired().HasMaxLength(2083);
            _ = builder.Property(ni => ni.Summary).HasColumnType("TEXT");
            _ = builder.Property(ni => ni.FullContent).HasColumnType("TEXT");
            _ = builder.Property(ni => ni.ImageUrl).HasMaxLength(2083);
            _ = builder.Property(ni => ni.PublishedDate).IsRequired();
            _ = builder.Property(ni => ni.CreatedAt).IsRequired();
            _ = builder.Property(ni => ni.LastProcessedAt);
            _ = builder.Property(ni => ni.SourceName).HasMaxLength(150);
            _ = builder.Property(ni => ni.SourceItemId).HasMaxLength(500);
            _ = builder.Property(ni => ni.SentimentScore); // C# double maps to PostgreSQL 'double precision'
            _ = builder.Property(ni => ni.SentimentLabel).HasMaxLength(50);
            _ = builder.Property(ni => ni.DetectedLanguage).HasMaxLength(10);
            _ = builder.Property(ni => ni.AffectedAssets).HasMaxLength(500);
            _ = builder.Property(ni => ni.RssSourceId).IsRequired();
            _ = builder.Property(ni => ni.AssociatedSignalCategoryId);
            _ = builder.Property(ni => ni.LinkHash).HasMaxLength(32).IsFixedLength(); // C# byte[] maps to PostgreSQL 'bytea'

            // --- BOOLEAN FIX & DEFAULT VALUE ---
            // For PostgreSQL, the type is 'boolean'. By not specifying a column type,
            // we let the EF Core provider choose the correct native type.
            _ = builder.Property(ni => ni.IsVipOnly)
                   .IsRequired()
                   .HasDefaultValue(false);

            // =================================================================
            // --- HIGH-PERFORMANCE INDEXES (with Summaries) ---
            // =================================================================

            // 1. DEDUPLICATION INDEX (CRITICAL)
            // Purpose: Prevents inserting the same article from the same feed.
            // Type: Filtered Unique Index
            // Query Optimized: Checks for existence before INSERT.
            // Why: This is the most important index for data integrity. The filter makes it efficient
            // by only indexing rows that have a SourceItemId.
            _ = builder.HasIndex(ni => new { ni.RssSourceId, ni.SourceItemId })
                   .IsUnique()
                   .HasFilter("\"SourceItemId\" IS NOT NULL") // PostgreSQL syntax
                   .HasDatabaseName("IX_NewsItems_RssSourceId_SourceItemId_Unique");

            // 2. PRIMARY SEARCH INDEX (USER-FACING PERFORMANCE)
            // Purpose: Speeds up the main query for displaying news to users.
            // Type: Composite Index
            // Query Optimized: `WHERE IsVipOnly = @p0 AND PublishedDate BETWEEN @p1 AND @p2 ORDER BY PublishedDate DESC`
            // Why: Allows the DB to quickly find news for a user's access level (VIP/non-VIP)
            // within a date range without scanning the whole table. The date is included for sorting.
            _ = builder.HasIndex(ni => new { ni.IsVipOnly, ni.PublishedDate })
                   .HasDatabaseName("IX_NewsItems_PrimarySearch");

            // 3. LINK HASH DEDUPLICATION INDEX
            // Purpose: Fast duplicate checking based on the article's link, across ALL sources.
            // Type: Filtered Unique Index
            // Query Optimized: `WHERE LinkHash = @p0`
            // Why: If you generate a hash of the link, this allows for extremely fast lookups
            // to see if an article has been posted before, even from a different RSS source.
            _ = builder.HasIndex(ni => ni.LinkHash)
                   .IsUnique()
                   .HasFilter("\"LinkHash\" IS NOT NULL")
                   .HasDatabaseName("IX_NewsItems_LinkHash_Unique");

            // 4. CATEGORY-BASED SEARCH INDEX
            // Purpose: Speeds up finding news within a specific category and date range.
            // Type: Composite Index
            // Query Optimized: `WHERE AssociatedSignalCategoryId = @p0 AND PublishedDate > @p1`
            // Why: Essential if you allow users to filter news by category.
            _ = builder.HasIndex(ni => new { ni.AssociatedSignalCategoryId, ni.PublishedDate })
                   .HasDatabaseName("IX_NewsItems_CategorySearch");

            // 5. BACKGROUND PROCESSING INDEX
            // Purpose: Helps background workers find unprocessed items efficiently.
            // Type: Filtered Index
            // Query Optimized: `WHERE LastProcessedAt IS NULL`
            // Why: Creates a small, highly efficient index containing only items that need
            // processing (e.g., for AI sentiment analysis), avoiding a full table scan.
            _ = builder.HasIndex(ni => ni.LastProcessedAt)
                   .HasFilter("\"LastProcessedAt\" IS NULL")
                   .HasDatabaseName("IX_NewsItems_Unprocessed");

            // 6. SOURCE-SPECIFIC SEARCH INDEX
            // Purpose: Improves performance when viewing the history of a single RSS source.
            // Type: Index
            // Query Optimized: `WHERE RssSourceId = @p0 ORDER BY PublishedDate DESC`
            // Why: While the Primary Search Index (2) helps, this is more direct and slightly
            // more efficient if you are only filtering by a single source.
            _ = builder.HasIndex(ni => ni.RssSourceId)
                   .HasDatabaseName("IX_NewsItems_BySource");

            // =================================================================
            // --- RELATIONSHIPS ---
            // =================================================================

            _ = builder.HasOne(ni => ni.RssSource)
                   .WithMany(rs => rs.NewsItems)
                   .HasForeignKey(ni => ni.RssSourceId)
                   .OnDelete(DeleteBehavior.Cascade);

            _ = builder.HasOne(ni => ni.AssociatedSignalCategory)
                   .WithMany()
                   .HasForeignKey(ni => ni.AssociatedSignalCategoryId)
                   .OnDelete(DeleteBehavior.SetNull);
        }
    }
}