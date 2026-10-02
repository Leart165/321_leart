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

    public DbSet<ProcessedTransaction> ProcessedTransactions
    {
        get { return Set<ProcessedTransaction>(); }
    }

    public DbSet<OwnerMonthly> OwnerMonthly
    {
        get { return Set<OwnerMonthly>(); }
    }

    public DbSet<SystemDaily> SystemDaily
    {
        get { return Set<SystemDaily>(); }
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
