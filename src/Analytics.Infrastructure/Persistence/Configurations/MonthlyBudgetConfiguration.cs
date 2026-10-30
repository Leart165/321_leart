using Analytics.Domain.Budgets;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Analytics.Infrastructure.Persistence.Configurations;

public sealed class MonthlyBudgetConfiguration : IEntityTypeConfiguration<MonthlyBudget>
{
    public void Configure(EntityTypeBuilder<MonthlyBudget> builder)
    {
        builder.ToTable("budgets");

        // Höchstens ein Budget je Kunde.
        builder.HasKey(budget => budget.OwnerId);
        builder.Property(budget => budget.OwnerId).HasColumnName("owner_id").HasMaxLength(100);

        builder.Ignore(budget => budget.Currency);
        builder.Property<string>("_currency").HasColumnName("currency").HasColumnType("char(3)").IsRequired();

        builder.Property(budget => budget.Limit).HasColumnName("monthly_limit").HasColumnType("numeric(19,2)");
        builder.Property(budget => budget.UpdatedAt).HasColumnName("updated_at");
    }
}
