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

            // ✅ CRITICAL FIX: Increased precision from 4 to 8 decimal places to support cryptocurrencies like BTC.
            _ = builder.Property(t => t.Amount)
                .IsRequired()
                .HasColumnType("decimal(18, 8)"); // Precision for crypto amounts

            // ✅ NEW: Configuration for the new Currency property.
            _ = builder.Property(t => t.Currency)
                .HasMaxLength(20); // e.g., "USDT", "BTC", "TON". 20 chars is plenty.
                                   // It is nullable by default, which is correct since we made it nullable in the entity.

            _ = builder.Property(t => t.Type)
                .IsRequired()
                .HasConversion(new EnumToStringConverter<TransactionType>());

            _ = builder.Property(t => t.Description)
                .HasMaxLength(500);

            _ = builder.Property(t => t.Timestamp)
                .IsRequired()
                .HasDefaultValueSql("NOW()");

            _ = builder.Property(t => t.PaymentGatewayInvoiceId)
                .HasMaxLength(100);

            _ = builder.Property(t => t.PaymentGatewayName)
                .HasMaxLength(50);

            _ = builder.Property(t => t.Status)
                .IsRequired()
                .HasMaxLength(50)
                .HasDefaultValue("Pending");

            _ = builder.Property(t => t.PaidAt);

            _ = builder.Property(t => t.PaymentGatewayPayload)
                .HasColumnType("TEXT");

            _ = builder.Property(t => t.PaymentGatewayResponse)
                .HasColumnType("TEXT");

            // Indexes
            _ = builder.HasIndex(t => t.UserId);
            _ = builder.HasIndex(t => t.PaymentGatewayInvoiceId);
            _ = builder.HasIndex(t => t.Status);
            _ = builder.HasIndex(t => t.Timestamp);
        }
    }
}