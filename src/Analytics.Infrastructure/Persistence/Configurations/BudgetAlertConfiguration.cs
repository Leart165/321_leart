using Analytics.Domain.Budgets;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Analytics.Infrastructure.Persistence.Configurations;

public sealed class BudgetAlertConfiguration : IEntityTypeConfiguration<BudgetAlert>
{
    public void Configure(EntityTypeBuilder<BudgetAlert> builder)
    {
        builder.ToTable("budget_alerts");

        builder.HasKey(alert => alert.Id);
        builder.Property(alert => alert.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(alert => alert.OwnerId).HasColumnName("owner_id").HasMaxLength(100).IsRequired();
        builder.Property(alert => alert.Year).HasColumnName("year");
        builder.Property(alert => alert.Month).HasColumnName("month");
        builder.Property(alert => alert.Level).HasColumnName("level").HasConversion<string>().HasMaxLength(20);
        builder.Property(alert => alert.Spent).HasColumnName("spent").HasColumnType("numeric(19,2)");
        builder.Property(alert => alert.Limit).HasColumnName("monthly_limit").HasColumnType("numeric(19,2)");

        builder.Ignore(alert => alert.Currency);
        builder.Property<string>("_currency").HasColumnName("currency").HasColumnType("char(3)").IsRequired();

        builder.Property(alert => alert.RaisedAt).HasColumnName("raised_at");

        // Je Kunde, Monat und Stufe höchstens eine Warnung, auch wenn zwei Instanzen zugleich prüfen.
        builder.HasIndex(alert => new { alert.OwnerId, alert.Year, alert.Month, alert.Level })
            .IsUnique()
            .HasDatabaseName("ux_budget_alerts_owner_month_level");
    }
}
