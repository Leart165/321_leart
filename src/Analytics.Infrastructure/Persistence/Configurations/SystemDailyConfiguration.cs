using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Analytics.Infrastructure.Persistence.Configurations;

public sealed class SystemDailyConfiguration : IEntityTypeConfiguration<SystemDaily>
{
    public void Configure(EntityTypeBuilder<SystemDaily> builder)
    {
        builder.ToTable("system_daily");

        builder.HasKey(daily => new { daily.Day, daily.Currency });

        builder.Property(daily => daily.Day)
            .HasColumnName("day")
            .HasColumnType("date")
            .IsRequired();

        builder.Property(daily => daily.Currency)
            .HasColumnName("currency")
            .HasColumnType("char(3)")
            .IsRequired();

        builder.Property(daily => daily.Volume)
            .HasColumnName("volume")
            .HasColumnType("numeric(19,2)")
            .IsRequired();

        builder.Property(daily => daily.Deposits)
            .HasColumnName("deposits")
            .IsRequired();

        builder.Property(daily => daily.Withdrawals)
            .HasColumnName("withdrawals")
            .IsRequired();

        builder.Property(daily => daily.Transfers)
            .HasColumnName("transfers")
            .IsRequired();
    }
}
