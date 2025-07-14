using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations
{
    /// <summary>
    /// Provides a complete and optimized database mapping for the ProMonitoringLog entity.
    /// Includes high-performance indexes for Postgres and compatibility for other providers.
    /// </summary>
    public class ProMonitoringLogConfiguration : IEntityTypeConfiguration<ProMonitoringLog>
    {
        public void Configure(EntityTypeBuilder<ProMonitoringLog> builder)
        {
            builder.ToTable("ProMonitoringLogs");
            builder.HasKey(l => l.Id);

            // Property configurations
            builder.Property(l => l.Level).IsRequired().HasMaxLength(20);
            builder.Property(l => l.Source).HasMaxLength(100);
            builder.Property(l => l.EventType).HasMaxLength(50);
            builder.Property(l => l.JobId).HasMaxLength(100);
            builder.Property(l => l.CorrelationId).HasMaxLength(100);
            builder.Property(l => l.UserId).HasMaxLength(100);
            builder.Property(l => l.Status).HasMaxLength(20);
            builder.Property(l => l.Message).IsRequired().HasMaxLength(500);
            builder.Property(l => l.Details).HasColumnType("TEXT");
            builder.Property(l => l.Exception).HasColumnType("TEXT");
            builder.Property(l => l.Tags).HasMaxLength(200);
            builder.Property(l => l.CreatedAt).IsRequired();
            builder.Property(l => l.UpdatedAt);
            builder.Property(l => l.Timestamp).IsRequired();

            // Indexes for fast querying
            builder.HasIndex(l => new { l.Level, l.Timestamp }).HasDatabaseName("IX_ProMonitoringLog_Level_Timestamp");
            builder.HasIndex(l => new { l.EventType, l.Timestamp }).HasDatabaseName("IX_ProMonitoringLog_EventType_Timestamp");
            builder.HasIndex(l => l.JobId).HasDatabaseName("IX_ProMonitoringLog_JobId");
            builder.HasIndex(l => l.CorrelationId).HasDatabaseName("IX_ProMonitoringLog_CorrelationId");
            builder.HasIndex(l => l.UserId).HasDatabaseName("IX_ProMonitoringLog_UserId");
            builder.HasIndex(l => l.Status).HasDatabaseName("IX_ProMonitoringLog_Status");
            // For Postgres: full-text index on Message/Details can be added via migration or raw SQL if needed

            // Additional composite indexes for pro-level performance
            builder.HasIndex(l => new { l.Source, l.EventType, l.Timestamp }).HasDatabaseName("IX_ProMonitoringLog_Source_EventType_Timestamp");
            builder.HasIndex(l => new { l.Level, l.Status, l.Timestamp }).HasDatabaseName("IX_ProMonitoringLog_Level_Status_Timestamp");
            builder.HasIndex(l => new { l.Tags, l.Timestamp }).HasDatabaseName("IX_ProMonitoringLog_Tags_Timestamp");

            // (Optional) Filtered index for logs with exceptions (Postgres only, add via migration or raw SQL)
            // builder.HasIndex(l => l.Exception).HasFilter("\"Exception\" IS NOT NULL").HasDatabaseName("IX_ProMonitoringLog_Exception_NotNull");

            // (Optional) Full-text index for Message/Details (Postgres only, add via migration or raw SQL)
            // CREATE INDEX idx_pro_monitoringlog_fulltext ON "ProMonitoringLogs" USING GIN (to_tsvector('english', "Message" || ' ' || coalesce("Details",'')));
        }
    }
} 