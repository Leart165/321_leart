using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Analytics.Infrastructure.Persistence.Configurations;

public sealed class OwnerMonthlyConfiguration : IEntityTypeConfiguration<OwnerMonthly>
{
    public void Configure(EntityTypeBuilder<OwnerMonthly> builder)
    {
        builder.ToTable("owner_monthly");

        builder.HasKey(monthly => new { monthly.OwnerId, monthly.Year, monthly.Month, monthly.Currency });

        builder.Property(monthly => monthly.OwnerId)
            .HasColumnName("owner_id")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(monthly => monthly.Year)
            .HasColumnName("year")
            .IsRequired();

        builder.Property(monthly => monthly.Month)
            .HasColumnName("month")
            .IsRequired();

        builder.Property(monthly => monthly.Currency)
            .HasColumnName("currency")
            .HasColumnType("char(3)")
            .IsRequired();

        builder.Property(monthly => monthly.Income)
            .HasColumnName("income")
            .HasColumnType("numeric(19,2)")
            .IsRequired();

        builder.Property(monthly => monthly.Expenses)
            .HasColumnName("expenses")
            .HasColumnType("numeric(19,2)")
            .IsRequired();

        builder.Property(monthly => monthly.Transactions)
            .HasColumnName("transactions")
            .IsRequired();
    }
}
