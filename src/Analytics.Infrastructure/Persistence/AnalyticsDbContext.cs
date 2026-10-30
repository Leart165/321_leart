using Analytics.Domain.Budgets;
using Analytics.Domain.Reports;
using Analytics.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace Analytics.Infrastructure.Persistence;

public sealed class AnalyticsDbContext : DbContext
{
    public AnalyticsDbContext(DbContextOptions<AnalyticsDbContext> options)
        : base(options)
    {
    }

    public DbSet<OwnerBooking> OwnerBookings
    {
        get { return Set<OwnerBooking>(); }
    }

    public DbSet<MonthlyReport> MonthlyReports
    {
        get { return Set<MonthlyReport>(); }
    }

    public DbSet<ReportDocumentRecord> ReportDocuments
    {
        get { return Set<ReportDocumentRecord>(); }
    }

    public DbSet<MonthlyBudget> Budgets
    {
        get { return Set<MonthlyBudget>(); }
    }

    public DbSet<BudgetAlert> BudgetAlerts
    {
        get { return Set<BudgetAlert>(); }
    }

    public DbSet<OutboxMessage> Outbox
    {
        get { return Set<OutboxMessage>(); }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}
