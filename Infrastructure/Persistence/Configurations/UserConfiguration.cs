// File: Infrastructure/Persistence/Configurations/UserConfiguration.cs
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Infrastructure.Persistence.Configurations
{
    /// <summary>
    /// Provides a complete and optimized database mapping for the User entity.
    /// This configuration is the single source of truth for the Users table schema.
    /// </summary>
    public class UserConfiguration : IEntityTypeConfiguration<User>
    {
        public void Configure(EntityTypeBuilder<User> builder)
        {
            _ = builder.ToTable("Users");
            _ = builder.HasKey(u => u.Id);

            // =================================================================
            // --- PROPERTY CONFIGURATIONS ---
            // =================================================================
            #region Property Configurations

            _ = builder.Property(u => u.Username).IsRequired().HasMaxLength(100);
            _ = builder.Property(u => u.TelegramId).IsRequired().HasMaxLength(50);
            _ = builder.Property(u => u.Email).IsRequired().HasMaxLength(200);
            _ = builder.Property(u => u.PreferredLanguage).IsRequired().HasMaxLength(10).HasDefaultValue("en");
            _ = builder.Property(u => u.CreatedAt).IsRequired();
            _ = builder.Property(u => u.UpdatedAt);

            // --- Converters & Default Values ---
            _ = builder.Property(u => u.Level)
                   .IsRequired()
                   .HasConversion(new EnumToStringConverter<UserLevel>());

            _ = builder.Property(u => u.EnableGeneralNotifications).IsRequired().HasDefaultValue(true);
            _ = builder.Property(u => u.EnableVipSignalNotifications).IsRequired().HasDefaultValue(false);
            _ = builder.Property(u => u.EnableRssNewsNotifications).IsRequired().HasDefaultValue(true);

            #endregion

            // =================================================================
            // --- HIGH-PERFORMANCE INDEXES (with Summaries) ---
            // =================================================================
            #region Indexes

            // 1. UNIQUE IDENTIFIER INDEXES (CRITICAL)
            // Purpose: Enforce data uniqueness and provide very fast lookups for individual users.
            // Why: Essential for authentication, finding a user by their Telegram ID, or preventing duplicate accounts.
            _ = builder.HasIndex(u => u.Username).IsUnique();
            _ = builder.HasIndex(u => u.TelegramId).IsUnique();
            _ = builder.HasIndex(u => u.Email).IsUnique();

            // 2. SIGNAL NOTIFICATION TARGETING INDEX (IMPORTANT)
            // Purpose: Massively speeds up finding users for signal push notifications.
            // Query Optimized: `WHERE Level = 'Vip' AND EnableVipSignalNotifications = true`
            // Why: Allows the notification dispatcher to get a list of target users
            // instantly, which is critical for sending time-sensitive signals.
            _ = builder.HasIndex(u => new { u.Level, u.EnableVipSignalNotifications })
                   .HasDatabaseName("IX_Users_NotificationTarget_Signal");

            // 3. NEWS NOTIFICATION TARGETING INDEX
            // Purpose: Quickly finds all users who have opted-in to receive any news.
            // Query Optimized: `WHERE EnableRssNewsNotifications = true`
            // Why: This is the first filter step. The system can get this small list of users
            // and then join against their specific preferences in the `UserRssPreferences` table.
            _ = builder.HasIndex(u => u.EnableRssNewsNotifications)
                   .HasDatabaseName("IX_Users_NotificationTarget_News");

            #endregion

            // =================================================================
            // --- RELATIONSHIPS ---
            // =================================================================
            #region Relationships

            _ = builder.HasOne(u => u.TokenWallet)
                   .WithOne(tw => tw.User)
                   .HasForeignKey<TokenWallet>(tw => tw.UserId)
                   .OnDelete(DeleteBehavior.Cascade); // Deleting a user deletes their wallet.

            _ = builder.HasMany(u => u.Subscriptions)
                   .WithOne(s => s.User)
                   .HasForeignKey(s => s.UserId)
                   .OnDelete(DeleteBehavior.Cascade); // Deleting a user deletes their subscriptions.

            _ = builder.HasMany(u => u.Transactions)
                   .WithOne(t => t.User)
                   .HasForeignKey(t => t.UserId)
                   .OnDelete(DeleteBehavior.Restrict); // Keep financial history even if user is deleted.

            // Many-to-Many for Signal Preferences
            _ = builder.HasMany(u => u.Preferences)
                   .WithOne(usp => usp.User)
                   .HasForeignKey(usp => usp.UserId)
                   .OnDelete(DeleteBehavior.Cascade); // Deleting a user deletes their signal preferences.

            // Many-to-Many for RSS Preferences (New)
            _ = builder.HasMany(u => u.RssPreferences)
                   .WithOne(urp => urp.User)
                   .HasForeignKey(urp => urp.UserId)
                   .OnDelete(DeleteBehavior.Cascade); // Deleting a user deletes their RSS preferences.

            #endregion
        }
    }
}