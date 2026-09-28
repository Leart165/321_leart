using Microsoft.Extensions.Options;

namespace Analytics.Api.Hosting;

// Bereitschaft für Docker und damit für Traefik: GET /ready auf dem internen Port, 200 oder 503.
// Kein Teil der API: nicht in Swagger, nicht im Kontrakt, keine Traces. Über Traefik nicht
// erreichbar, weil der Endpunkt nur auf Anfragen an den internen Port antwortet.
public static class Readiness
{
    public const string Path = "/ready";

    public static IServiceCollection AddReadiness(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ReadinessOptions>(configuration.GetSection(ReadinessOptions.SectionName));
        services.AddSingleton<ReadinessState>();
        services.AddHostedService<DrainOnShutdown>();
        return services;
    }

    public static IEndpointConventionBuilder MapReadiness(this WebApplication app)
    {
        ReadinessOptions options = app.Services.GetRequiredService<IOptions<ReadinessOptions>>().Value;

        return app.MapGet(Path, (ReadinessState readiness) => readiness.IsReady
                ? Results.Text("ready")
                : Results.Text("draining", statusCode: StatusCodes.Status503ServiceUnavailable))
            .RequireHost($"*:{options.Port}")
            .ExcludeFromDescription()
            .DisableHttpMetrics();
    }
}

public sealed class ReadinessOptions
{
    public const string SectionName = "Readiness";

    // Nur im Container und für Docker erreichbar; Traefik routet auf 8080.
    public int Port { get; set; } = 8081;

    // So lange bedient eine Instanz nach SIGTERM weiter, während sie schon "nicht bereit" meldet.
    // Das Dockerfile setzt sie; in Tests und bei dotnet run fliesst nichts ab.
    public TimeSpan DrainDuration { get; set; } = TimeSpan.Zero;
}

// Bewusst ohne Datenbank und Broker: fällt eine gemeinsame Abhängigkeit aus, soll Traefik nicht
// alle Instanzen aus der Verteilung nehmen. Die Aufrufer bekommen dann die eigene 503 des
// Dienstes mit Retry-After. Das prüft /health, nicht /ready.
public sealed class ReadinessState
{
    private volatile bool _draining;

    public bool IsReady
    {
        get { return !_draining; }
    }

    public void MarkDraining()
    {
        _draining = true;
    }
}

// Bei SIGTERM sofort "nicht bereit", dann weiter bedienen, bis Docker die Instanz als ungesund
// meldet und Traefik sie herausnimmt. Erst danach stoppen Webserver und Konsumenten.
public sealed class DrainOnShutdown : IHostedLifecycleService
{
    private readonly ReadinessState _readiness;
    private readonly ReadinessOptions _options;
    private readonly ILogger<DrainOnShutdown> _logger;

    public DrainOnShutdown(ReadinessState readiness, IOptions<ReadinessOptions> options, ILogger<DrainOnShutdown> logger)
    {
        _readiness = readiness;
        _options = options.Value;
        _logger = logger;
    }

    public async Task StoppingAsync(CancellationToken cancellationToken)
    {
        _readiness.MarkDraining();
        if (_options.DrainDuration <= TimeSpan.Zero)
        {
            return;
        }

        _logger.LogInformation(
            "Fährt herunter: meldet nicht mehr bereit und bedient noch {DrainDuration}, bis Traefik die Instanz herausnimmt.",
            _options.DrainDuration);

        try
        {
            await Task.Delay(_options.DrainDuration, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Die Frist des Hosts ist abgelaufen; dann wird jetzt gestoppt.
        }
    }

    public Task StartingAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task StartedAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task StoppedAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
