using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Auth.Infrastructure.Persistence;

public sealed class DatabaseMigrator : IHostedService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<DatabaseMigrator> _logger;

    public DatabaseMigrator(IServiceProvider services, ILogger<DatabaseMigrator> logger)
    {
        _services = services;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Schema wird auf den Stand des Codes gebracht.");
        await _services.MigrateDatabaseAsync(cancellationToken);
        _logger.LogInformation("Schema ist aktuell.");
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
