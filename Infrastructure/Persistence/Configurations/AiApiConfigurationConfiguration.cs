using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations
{
    /// <summary>
    /// Provides a complete and optimized database mapping for the AiApiConfiguration entity.
    /// This configuration is the single source of truth for the AiApiConfigurations table schema,
    /// including properties, column types, default values, and a comprehensive
    /// set of high-performance indexes tailored for common query patterns.
    /// </summary>
    public class AiApiConfigurationConfiguration : IEntityTypeConfiguration<AiApiConfiguration>
    {
        public void Configure(EntityTypeBuilder<AiApiConfiguration> builder)
        {
            // By convention, your DbContext's UseSnakeCaseNamingConvention() will handle this,
            // but being explicit helps clarity.
            _ = builder.ToTable("AiApiConfigurations");

            _ = builder.HasKey(c => c.Id);

            // =================================================================
            // --- PROPERTY CONFIGURATIONS ---
            // =================================================================
            #region Property Configurations

            _ = builder.Property(c => c.ProviderName)
                .IsRequired()
                .HasMaxLength(50);

            _ = builder.Property(c => c.ModelName)
                .IsRequired()
                .HasMaxLength(100);

            // Use TEXT for fields that could be long, like API keys and prompts.
            _ = builder.Property(c => c.ApiKey)
                .IsRequired()
                .HasColumnType("TEXT");

            _ = builder.Property(c => c.PromptTemplate)
                .IsRequired()
                .HasColumnType("TEXT");

            _ = builder.Property(c => c.Description).HasMaxLength(500);

            // Set database-level defaults for new records.
            _ = builder.Property(c => c.IsEnabled)
                .IsRequired()
                .HasDefaultValue(true);

            _ = builder.Property(c => c.CreatedAt)
                .IsRequired()
                .HasDefaultValueSql("NOW()"); // PostgreSQL-specific function for current UTC time.

            _ = builder.Property(c => c.LastUpdatedAt)
                .IsRequired()
                .HasDefaultValueSql("NOW()");

            #endregion

            // =================================================================
            // --- HIGH-PERFORMANCE INDEXES (The "Strong Table" part) ---
            // =================================================================
            #region Indexes

            // 1. PRIMARY LOOKUP INDEX (CRITICAL)
            // Purpose: Massively speeds up the most frequent query: finding the active
            // configuration for a specific service (e.g., "Get me the active Gemini config").
            // Type: Unique Composite Index.
            // Query Optimized: `WHERE ProviderName = @p0 AND IsEnabled = true`
            // Why it's powerful: The uniqueness on `ProviderName` enforces data integrity (no two
            // configs for "Gemini"). The composite nature allows the database to instantly
            // locate the exact record using the index without extra filtering steps.
            _ = builder.HasIndex(c => new { c.ProviderName, c.IsEnabled })
                   .HasDatabaseName("IX_AiApiConfigurations_ProviderName_IsEnabled");

            // 2. UNIQUE PROVIDER NAME INDEX (DATA INTEGRITY)
            // Purpose: Guarantees that you can never accidentally insert two configurations
            // for the same provider, regardless of their IsEnabled status.
            // Type: Unique Index.
            // Query Optimized: `WHERE ProviderName = @p0`
            // Why it's powerful: This is a database-level safeguard against bad data. It also
            // makes lookups by just the provider name extremely fast for admin panels.
            _ = builder.HasIndex(c => c.ProviderName)
                   .IsUnique()
                   .HasDatabaseName("IX_AiApiConfigurations_ProviderName_Unique");

            // 3. ACTIVE STATUS INDEX (ADMIN/MONITORING)
            // Purpose: Quickly find all configurations that are currently enabled or disabled.
            // Type: Non-unique Index.
            // Query Optimized: `WHERE IsEnabled = true`
            // Why it's powerful: Useful for an administrative dashboard to quickly show a list
            // of all active integrations without scanning the entire table.
            _ = builder.HasIndex(c => c.IsEnabled)
                   .HasDatabaseName("IX_AiApiConfigurations_IsEnabled");

            #endregion
        }
    }
}