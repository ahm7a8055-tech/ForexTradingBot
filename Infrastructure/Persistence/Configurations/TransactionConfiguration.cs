// File: Infrastructure/Persistence/Configurations/TransactionConfiguration.cs
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Infrastructure.Persistence.Configurations
{
    public class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
    {
        public void Configure(EntityTypeBuilder<Transaction> builder)
        {
            _ = builder.ToTable("Transactions");
            _ = builder.HasKey(t => t.Id);

            _ = builder.Property(t => t.UserId).IsRequired();

            _ = builder.Property(t => t.Amount)
                .IsRequired()
                .HasColumnType("decimal(18, 4)"); // Precision for transaction amounts

            _ = builder.Property(t => t.Type)
                .IsRequired()
                .HasConversion(new EnumToStringConverter<TransactionType>());

            _ = builder.Property(t => t.Description)
                .HasMaxLength(500); // Optional

            _ = builder.Property(t => t.Timestamp) // Renamed from CreatedAt in your previous DbContext to avoid confusion with audit fields
                .IsRequired()
                .HasDefaultValueSql("NOW()"); // Keeping this as it's correct for PostgreSQL

            // New fields for payment gateway integration
            _ = builder.Property(t => t.PaymentGatewayInvoiceId)
                .HasMaxLength(100);
            _ = builder.Property(t => t.PaymentGatewayName)
                .HasMaxLength(50);

            _ = builder.Property(t => t.Status)
                .IsRequired()
                .HasMaxLength(50)
                .HasDefaultValue("Pending");

            _ = builder.Property(t => t.PaidAt); // Nullable DateTime

            // --- FIX APPLIED HERE ---
            _ = builder.Property(t => t.PaymentGatewayPayload)
                .HasColumnType("TEXT"); // Changed from "nvarchar(max)" to "TEXT" for PostgreSQL

            _ = builder.Property(t => t.PaymentGatewayResponse)
                .HasColumnType("TEXT"); // Changed from "nvarchar(max)" to "TEXT" for PostgreSQL
            // --- END FIX ---

            // Indexes
            _ = builder.HasIndex(t => t.UserId);
            _ = builder.HasIndex(t => t.PaymentGatewayInvoiceId); // Not necessarily unique if retries create new internal transactions for same gateway ID
            _ = builder.HasIndex(t => t.Status);
            _ = builder.HasIndex(t => t.Timestamp);
        }
    }
}