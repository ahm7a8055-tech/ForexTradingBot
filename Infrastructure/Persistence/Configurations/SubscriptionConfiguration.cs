// File: Infrastructure/Persistence/Configurations/SubscriptionConfiguration.cs
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations
{
    /// <summary>
    /// Provides a complete and optimized database mapping for the Subscription entity.
    /// </summary>
    public class SubscriptionConfiguration : IEntityTypeConfiguration<Subscription>
    {
        public void Configure(EntityTypeBuilder<Subscription> builder)
        {
            builder.ToTable("Subscriptions");
            builder.HasKey(s => s.Id);

            // --- Property Configurations ---
            builder.Property(s => s.UserId).IsRequired();
            builder.Property(s => s.StartDate).IsRequired();
            builder.Property(s => s.EndDate).IsRequired();
            builder.Property(s => s.CreatedAt).IsRequired().HasDefaultValueSql("NOW()"); // For PostgreSQL

            // This property is a calculated value in the entity and has no database column.
            // Explicitly ignoring it is a best practice.
            builder.Ignore(s => s.IsCurrentlyActive);

            // =================================================================
            // --- HIGH-PERFORMANCE INDEXES ---
            // =================================================================

            // 1. PRIMARY USER SUBSCRIPTION CHECK (CRITICAL)
            // Purpose: Massively speeds up the most common query: "Is this user currently subscribed?"
            // Type: Composite Index
            // Query Optimized: `WHERE UserId = @p0 AND EndDate > GETDATE()`
            // Why: This index allows the database to instantly find all subscriptions for a user
            // and then filter them by the end date without a full table scan. This is essential
            // for any authorization check that depends on an active subscription.
            builder.HasIndex(s => new { s.UserId, s.EndDate })
                   .HasDatabaseName("IX_Subscriptions_CheckIsActive");

            // 2. EXPIRATION PROCESSING INDEX
            // Purpose: Helps a background job find subscriptions that have recently expired.
            // Type: Index
            // Query Optimized: `WHERE EndDate < GETDATE()`
            // Why: Useful for a worker that needs to downgrade users from VIP to Free
            // after their subscription ends.
            builder.HasIndex(s => s.EndDate)
                   .HasDatabaseName("IX_Subscriptions_ByEndDate");

            // =================================================================
            // --- RELATIONSHIPS ---
            // =================================================================
            builder.HasOne(s => s.User)
                   .WithMany(u => u.Subscriptions)
                   .HasForeignKey(s => s.UserId)
                   .OnDelete(DeleteBehavior.Cascade); // Deleting a user also deletes their subscription history.
        }
    }
}