using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Analytics.Infrastructure.Observability;

public static class ObservabilityExtensions
{
    public const string EndpointVariable = "OTEL_EXPORTER_OTLP_ENDPOINT";

    private const int ExportIntervalMilliseconds = 5000;

    private static readonly string[] Meters =
    {
        "Microsoft.AspNetCore.Hosting",
        "Microsoft.AspNetCore.Server.Kestrel",
        "System.Net.Http",
        "System.Runtime",
        "Analytics.*"
    };

    private static readonly string[] TraceSources =
    {
        "Analytics.*",
        StandaloneDatabaseSpanFilter.NpgsqlSource
    };

    // Ohne Sammler passiert nichts: Tests und ein Start ohne Alloy senden nicht ins Leere.
    public static IHostApplicationBuilder AddAnalyticsObservability(this IHostApplicationBuilder builder, string serviceName)
    {
        if (string.IsNullOrWhiteSpace(builder.Configuration[EndpointVariable]))
        {
            return builder;
        }

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(
                serviceName,
                serviceInstanceId: Environment.MachineName))
            .WithMetrics(metrics => metrics
                .AddMeter(Meters)
                .AddOtlpExporter((_, reader) =>
                {
                    reader.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = ExportIntervalMilliseconds;
                }))
            .WithTracing(tracing => tracing
                .AddSource(TraceSources)
                .AddHttpClientInstrumentation()
                .AddProcessor(new StandaloneDatabaseSpanFilter())
                .AddOtlpExporter());

        return builder;
    }
}
