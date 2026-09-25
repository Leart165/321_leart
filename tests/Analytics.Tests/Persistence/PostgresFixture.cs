using Analytics.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace Analytics.Tests.Persistence;

public sealed class PostgresFixture : IAsyncLifetime
{
    private static readonly string AdminConnectionString =
        Environment.GetEnvironmentVariable("ANALYTICS_TEST_CONNECTION")
        ?? "Host=localhost;Port=5435;Database=postgres;Username=analytics;Password=analytics";

    private readonly string _databaseName = "analytics_test_" + Guid.NewGuid().ToString("N");

    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        await using (NpgsqlConnection admin = new NpgsqlConnection(AdminConnectionString))
        {
            await admin.OpenAsync();
            await using NpgsqlCommand create = admin.CreateCommand();
            create.CommandText = $"CREATE DATABASE \"{_databaseName}\"";
            await create.ExecuteNonQueryAsync();
        }

        NpgsqlConnectionStringBuilder builder = new NpgsqlConnectionStringBuilder(AdminConnectionString)
        {
            Database = _databaseName
        };
        ConnectionString = builder.ConnectionString;

        await using AnalyticsDbContext context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public AnalyticsDbContext CreateContext()
    {
        DbContextOptionsBuilder<AnalyticsDbContext> options = new DbContextOptionsBuilder<AnalyticsDbContext>();
        options.UseNpgsql(ConnectionString);
        return new AnalyticsDbContext(options.Options);
    }

    public async Task ResetAsync()
    {
        await using AnalyticsDbContext context = CreateContext();
        await context.Database.ExecuteSqlRawAsync(
            "TRUNCATE processed_transactions, owner_monthly, system_daily");
    }

    public async Task DisposeAsync()
    {
        NpgsqlConnection.ClearAllPools();

        await using NpgsqlConnection admin = new NpgsqlConnection(AdminConnectionString);
        await admin.OpenAsync();
        await using NpgsqlCommand drop = admin.CreateCommand();
        drop.CommandText = $"DROP DATABASE IF EXISTS \"{_databaseName}\" WITH (FORCE)";
        await drop.ExecuteNonQueryAsync();
    }
}
