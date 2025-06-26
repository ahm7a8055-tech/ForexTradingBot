// File: Infrastructure/Persistence/Configurations/SignalAnalysisConfiguration.cs
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations
{
    public class SignalAnalysisConfiguration : IEntityTypeConfiguration<SignalAnalysis>
    {
        public void Configure(EntityTypeBuilder<SignalAnalysis> builder)
        {
            _ = builder.ToTable("SignalAnalyses");
            _ = builder.HasKey(sa => sa.Id);

            _ = builder.Property(sa => sa.SignalId).IsRequired(); // FK to Signal

            _ = builder.Property(sa => sa.AnalystName)
                .IsRequired()
                .HasMaxLength(150);

            // --- FIX APPLIED HERE ---
            _ = builder.Property(sa => sa.AnalysisText) // Renamed from Notes for clarity
                .IsRequired()
                .HasColumnType("TEXT"); // Changed to "TEXT" for PostgreSQL
            // --- END FIX ---

            _ = builder.Property(sa => sa.SentimentScore); // Nullable double or decimal

            _ = builder.Property(sa => sa.CreatedAt).IsRequired();

            // Relationship with Signal is configured by Signal entity's HasMany
        }
    }
}