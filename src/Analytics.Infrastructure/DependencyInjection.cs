using Analytics.Application.Abstractions;
using Analytics.Application.Ledger;
using Analytics.Infrastructure.Messaging;
using Analytics.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Data;
using System.Data.Common;

namespace Analytics.Infrastructure;

public static class DependencyInjection
{
    public const string ConnectionStringName = "AnalyticsDb";

    public static IServiceCollection AddAnalyticsCore(this IServiceCollection services, IConfiguration configuration)
    {
        string connectionString = configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException(
                $"Der Konfigurationswert 'ConnectionStrings:{ConnectionStringName}' fehlt.");

        services.AddDbContext<AnalyticsDbContext>(options => options.UseNpgsql(connectionString));

        services.AddScoped<ITotalsStore, TotalsStore>();
        services.AddScoped<LedgerProjection>();
        services.AddScoped<IMonthlyTotalsReader, MonthlyTotalsReader>();
        services.AddScoped<IDailyTotalsReader, DailyTotalsReader>();

        services.Configure<MessagingOptions>(configuration.GetSection(MessagingOptions.SectionName));
        services.AddSingleton<RabbitMqConnection>();

        return services;
    }

    public static IServiceCollection AddDatabaseMigration(this IServiceCollection services)
    {
        services.AddHostedService<DatabaseMigrator>();
        return services;
    }

    public static IServiceCollection AddLedgerConsumer(this IServiceCollection services)
    {
        services.AddHostedService<TransactionCompletedConsumer>();
        return services;
    }

    private const long MigrationLockKey = 8_321_0003;

    public static async Task MigrateDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken)
    {
        using IServiceScope scope = services.CreateScope();
        AnalyticsDbContext context = scope.ServiceProvider.GetRequiredService<AnalyticsDbContext>();

        DbConnection connection = context.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await ExecuteAsync(connection, $"SELECT pg_advisory_lock({MigrationLockKey})", cancellationToken);
        try
        {
            await context.Database.MigrateAsync(cancellationToken);
        }
        finally
        {
            await ExecuteAsync(connection, $"SELECT pg_advisory_unlock({MigrationLockKey})", cancellationToken);
        }
    }

    private static async Task ExecuteAsync(DbConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using DbCommand command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
