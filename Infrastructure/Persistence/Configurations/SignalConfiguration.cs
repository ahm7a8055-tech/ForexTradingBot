// File: Infrastructure/Persistence/Configurations/SignalConfiguration.cs
#region Usings
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
#endregion

namespace Infrastructure.Persistence.Configurations
{
    /// <summary>
    /// Provides a complete and optimized database mapping for the Signal entity.
    /// This configuration is the single source of truth for the Signals table schema,
    /// including properties, column types, default values, relationships, and a comprehensive
    /// set of high-performance indexes tailored for common query patterns.
    /// </summary>
    public class SignalConfiguration : IEntityTypeConfiguration<Signal>
    {
        public void Configure(EntityTypeBuilder<Signal> builder)
        {
            builder.ToTable("Signals");
            builder.HasKey(s => s.Id);

            // =================================================================
            // --- PROPERTY CONFIGURATIONS ---
            // =================================================================
            #region Property Configurations

            // --- Enums and Text ---
            builder.Property(s => s.Type)
                   .IsRequired()
                   .HasConversion(new EnumToStringConverter<SignalType>());

            builder.Property(s => s.Symbol)
                   .IsRequired()
                   .HasMaxLength(50);

            builder.Property(s => s.SourceProvider)
                   .IsRequired()
                   .HasMaxLength(100);

            builder.Property(s => s.Status)
                   .IsRequired()
                   .HasConversion(new EnumToStringConverter<SignalStatus>())
                   .HasDefaultValue(SignalStatus.Pending); // New signals are always Pending.

            builder.Property(s => s.Timeframe).HasMaxLength(10);
            builder.Property(s => s.Notes).HasMaxLength(1000);

            // --- Financial Decimals ---
            // Explicitly setting precision is crucial for financial data to prevent rounding errors.
            builder.Property(s => s.EntryPrice).HasColumnType("decimal(18, 8)").IsRequired();
            builder.Property(s => s.StopLoss).HasColumnType("decimal(18, 8)").IsRequired();
            builder.Property(s => s.TakeProfit).HasColumnType("decimal(18, 8)").IsRequired();
            // Example for future Take Profit levels:
            // builder.Property(s => s.TakeProfit2).HasColumnType("decimal(18, 8)");
            // builder.Property(s => s.TakeProfit3).HasColumnType("decimal(18, 8)");

            // --- Booleans ---
            builder.Property(s => s.IsVipOnly)
                   .IsRequired()
                   .HasDefaultValue(false); // New signals are public by default.

            // --- Timestamps ---
            builder.Property(s => s.PublishedAt).IsRequired();
            builder.Property(s => s.UpdatedAt);
            builder.Property(s => s.ClosedAt);

            #endregion

            // =================================================================
            // --- HIGH-PERFORMANCE INDEXES (with Summaries) ---
            // =================================================================
            #region Indexes

            // 1. PRIMARY SEARCH & NOTIFICATION INDEX (CRITICAL)
            // Purpose: Massively speeds up finding relevant, active signals for user notifications.
            // Type: Composite Index
            // Query Optimized: `WHERE CategoryId=@p0 AND IsVipOnly=@p1 AND Status='Pending' ORDER BY PublishedAt DESC`
            // Why: This is the most important index for performance. It allows the dispatcher to
            // instantly find new signals for specific user groups without a full table scan.
            builder.HasIndex(s => new { s.CategoryId, s.IsVipOnly, s.Status, s.PublishedAt })
                   .HasDatabaseName("IX_Signals_PrimarySearch");

            // 2. ACTIVE SIGNAL MONITORING INDEX
            // Purpose: Helps background jobs efficiently monitor active signals.
            // Type: Index on an Enum (converted to string)
            // Query Optimized: `WHERE Status = 'Active'`
            // Why: A worker process that checks if active signals have hit their TP/SL
            // can use this small, efficient index instead of scanning all signals.
            builder.HasIndex(s => s.Status)
                   .HasDatabaseName("IX_Signals_ByStatus");

            // 3. SYMBOL-BASED LOOKUP INDEX
            // Purpose: Fast retrieval of all signals for a specific symbol (e.g., "EURUSD").
            // Type: Standard Index
            // Query Optimized: `WHERE Symbol = @p0`
            // Why: Useful for historical analysis or displaying a chart with all signals for one pair.
            builder.HasIndex(s => s.Symbol)
                   .HasDatabaseName("IX_Signals_BySymbol");

            #endregion

            // =================================================================
            // --- RELATIONSHIPS ---
            // =================================================================
            #region Relationships

            // Defines the many-to-one relationship with SignalCategory.
            builder.HasOne(s => s.Category)
                   .WithMany(sc => sc.Signals)
                   .HasForeignKey(s => s.CategoryId)
                   .OnDelete(DeleteBehavior.Restrict); // Important: Prevents deleting a Category if it still has associated Signals.

            // Defines the one-to-many relationship with SignalAnalysis.
            builder.HasMany(s => s.Analyses)
                   .WithOne(sa => sa.Signal)
                   .HasForeignKey(sa => sa.SignalId)
                   .OnDelete(DeleteBehavior.Cascade); // If a Signal is deleted, all of its Analyses are also deleted.

            #endregion
        }
    }
}