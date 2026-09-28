using OpenTelemetry.Trace;

namespace Analytics.Api.Observability;

public static class ServerTracing
{
    // Wirkt nur, wenn AddAnalyticsObservability OpenTelemetry eingeschaltet hat.
    public static IServiceCollection AddServerTracing(this IServiceCollection services)
    {
        services.ConfigureOpenTelemetryTracerProvider(tracing => tracing
            .AddAspNetCoreInstrumentation(options =>
            {
                options.Filter = context => IsBusinessRequest(context.Request.Path);
            }));

        return services;
    }

    // Nur Fachanfragen beginnen einen Trace. Bereitschaft, Swagger und die Dateien der
    // Statistikseite würden sonst die Buchungen in Tempo überdecken.
    private static bool IsBusinessRequest(PathString path)
    {
        return path.StartsWithSegments("/v1") || path.StartsWithSegments("/v2");
    }
}
