// File: Infrastructure/Persistence/Configurations/ForwardingRuleConfiguration.cs

#region Usings
// ... (Usings are correct and remain the same) ...
using Domain.Entities;
using Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System.Text.Json;
using Domain.Features.Forwarding.Entities;
#endregion

namespace Infrastructure.Persistence.Configurations
{
    public class ForwardingRuleConfiguration : IEntityTypeConfiguration<ForwardingRule>
    {
        private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = false };

        public void Configure(EntityTypeBuilder<ForwardingRule> builder)
        {
            builder.ToTable("ForwardingRules");
            builder.HasKey(fr => fr.RuleName);

            // ... (Core properties and TargetChannelIds config are correct) ...
            builder.Property(fr => fr.RuleName).IsRequired().HasMaxLength(100);
            builder.Property(fr => fr.IsEnabled).IsRequired().HasDefaultValue(true);
            builder.Property(fr => fr.SourceChannelId).IsRequired();
            builder.Property(fr => fr.TargetChannelIds)
                   .HasColumnType("jsonb")
                   .HasConversion(
                       v => JsonSerializer.Serialize(v ?? new List<long>(), _jsonOptions),
                       v => JsonSerializer.Deserialize<List<long>>(v, _jsonOptions) ?? new List<long>()
                   )
                   .Metadata.SetValueComparer(CreateValueComparer<long>());

            // --- Configure Owned Types ---
            builder.OwnsOne(fr => fr.EditOptions, editOptionsBuilder =>
            {
                // ... (This part was correct and remains the same) ...
                editOptionsBuilder.OwnsMany(e => e.TextReplacements, replacementBuilder =>
                {
                    replacementBuilder.ToTable("ForwardingRuleTextReplacements");
                    replacementBuilder.WithOwner().HasForeignKey("ForwardingRuleName");
                    replacementBuilder.Property<int>("Id").ValueGeneratedOnAdd();
                    replacementBuilder.HasKey("Id");
                    replacementBuilder.Property(t => t.Find).IsRequired();
                });
            });

            // Configure MessageFilterOptions as an owned entity.
            builder.OwnsOne(fr => fr.FilterOptions, filterOptionsBuilder =>
            {
                // First, configure the properties of the owned type.
                ConfigureJsonbList(filterOptionsBuilder.Property(f => f.AllowedMessageTypes));
                ConfigureJsonbList(filterOptionsBuilder.Property(f => f.AllowedMimeTypes));
                ConfigureJsonbList(filterOptionsBuilder.Property(f => f.AllowedSenderUserIds));
                ConfigureJsonbList(filterOptionsBuilder.Property(f => f.BlockedSenderUserIds));

                // --- FIX APPLIED HERE: Define indexes on the OWNED TYPE's builder ---

                // 3. TEXT SEARCH ON FILTER (PostgreSQL Trigram Index)
                // We define the index here, on the builder for FilterOptions.
                filterOptionsBuilder.HasIndex(f => f.ContainsText)
                    .HasMethod("gist")
                    .HasOperators("gist_trgm_ops")
                    .HasDatabaseName("IX_ForwardingRules_ContainsText_Trgm");

                // 4. SENDER ID FILTER (PostgreSQL GIN Index)
                filterOptionsBuilder.HasIndex(f => f.AllowedSenderUserIds)
                    .HasMethod("gin")
                    .HasDatabaseName("IX_ForwardingRules_AllowedSenders_GIN");

                // 5. MESSAGE TYPE FILTER (PostgreSQL GIN Index)
                filterOptionsBuilder.HasIndex(f => f.AllowedMessageTypes)
                    .HasMethod("gin")
                    .HasDatabaseName("IX_ForwardingRules_MessageTypes_GIN");
            });

            // =================================================================
            // --- INDEXES on the ROOT ENTITY (ForwardingRule) ---
            // =================================================================
            #region Indexes

            // These indexes reference properties directly on ForwardingRule, so they stay here.

            // 1. PRIMARY RULE LOOKUP INDEX (CRITICAL)
            builder.HasIndex(fr => new { fr.SourceChannelId, fr.IsEnabled })
                   .HasDatabaseName("IX_ForwardingRules_BySourceChannelAndStatus");

            // 2. TARGET CHANNEL LOOKUP INDEX (PostgreSQL GIN Index)
            builder.HasIndex(fr => fr.TargetChannelIds)
                   .HasMethod("gin")
                   .HasDatabaseName("IX_ForwardingRules_ByTargetChannel_GIN");

            // 6. GENERAL STATUS LOOKUP
            builder.HasIndex(fr => fr.IsEnabled)
                   .HasDatabaseName("IX_ForwardingRules_IsEnabled");

            #endregion
        }

        #region Helper Methods
        // Helper methods remain the same...
        private static ValueComparer<IReadOnlyList<T>> CreateValueComparer<T>()
        {
            return new ValueComparer<IReadOnlyList<T>>(
                (c1, c2) => (c1 == null && c2 == null) || (c1 != null && c2 != null && c1.SequenceEqual(c2)),
                c => c == null ? 0 : c.Aggregate(0, (a, v) => HashCode.Combine(a, v == null ? 0 : v.GetHashCode())),
                c => c == null ? new List<T>() : c.ToList()
            );
        }

        private static void ConfigureJsonbList<T>(PropertyBuilder<IReadOnlyList<T>> propertyBuilder)
        {
            propertyBuilder
                .HasColumnType("jsonb")
                .HasConversion(
                    v => JsonSerializer.Serialize(v ?? new List<T>(), _jsonOptions),
                    v => JsonSerializer.Deserialize<List<T>>(v, _jsonOptions) ?? new List<T>()
                )
                .Metadata.SetValueComparer(CreateValueComparer<T>());
        }
        #endregion
    }
}