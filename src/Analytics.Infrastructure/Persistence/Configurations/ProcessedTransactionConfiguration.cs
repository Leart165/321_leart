using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Analytics.Infrastructure.Persistence.Configurations;

public sealed class ProcessedTransactionConfiguration : IEntityTypeConfiguration<ProcessedTransaction>
{
    public void Configure(EntityTypeBuilder<ProcessedTransaction> builder)
    {
        builder.ToTable("processed_transactions");

        builder.HasKey(processed => processed.TransactionId);

        builder.Property(processed => processed.TransactionId)
            .HasColumnName("transaction_id")
            .ValueGeneratedNever();

        builder.Property(processed => processed.ProcessedAt)
            .HasColumnName("processed_at")
            .IsRequired();
    }
}
