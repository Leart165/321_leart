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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}
