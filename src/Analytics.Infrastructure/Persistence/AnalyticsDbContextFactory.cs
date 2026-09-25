using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Analytics.Infrastructure.Persistence;

public sealed class AnalyticsDbContextFactory : IDesignTimeDbContextFactory<AnalyticsDbContext>
{
    private const string LocalDevelopmentConnection =
        "Host=localhost;Port=5435;Database=analytics;Username=analytics;Password=analytics";

    public AnalyticsDbContext CreateDbContext(string[] args)
    {
        string connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__AnalyticsDb")
            ?? LocalDevelopmentConnection;

        DbContextOptionsBuilder<AnalyticsDbContext> builder = new DbContextOptionsBuilder<AnalyticsDbContext>();
        builder.UseNpgsql(connectionString);

        return new AnalyticsDbContext(builder.Options);
    }
}
