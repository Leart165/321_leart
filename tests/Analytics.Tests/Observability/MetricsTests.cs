using Analytics.Infrastructure.Messaging;
using Analytics.Infrastructure.Observability;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using System.Diagnostics.Metrics;
using Xunit;

namespace Analytics.Tests.Observability;

public sealed class MetricsTests
{
    [Fact]
    public void Without_a_collector_nothing_is_exported()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings());
        builder.Configuration[ObservabilityExtensions.EndpointVariable] = null;

        builder.AddAnalyticsObservability("analytics-api");
        using IHost host = builder.Build();

        Assert.Null(host.Services.GetService<MeterProvider>());
        Assert.Null(host.Services.GetService<TracerProvider>());
    }

    [Fact]
    public void With_a_collector_metrics_and_traces_are_exported()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings());
        builder.Configuration[ObservabilityExtensions.EndpointVariable] = "http://alloy:4317";

        builder.AddAnalyticsObservability("analytics-api");
        using IHost host = builder.Build();

        Assert.NotNull(host.Services.GetService<MeterProvider>());
        Assert.NotNull(host.Services.GetService<TracerProvider>());
    }

    [Fact]
    public void Consumed_messages_are_counted_by_queue_and_outcome()
    {
        List<(long Value, Dictionary<string, object?> Tags)> measurements = new List<(long, Dictionary<string, object?>)>();

        using MeterListener listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == MessagingMetrics.MeterName && instrument.Name == "bank.messages.consumed")
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((_, value, tags, _) =>
        {
            Dictionary<string, object?> copy = tags.ToArray().ToDictionary(tag => tag.Key, tag => tag.Value);
            lock (measurements)
            {
                measurements.Add((value, copy));
            }
        });
        listener.Start();

        MessagingMetrics.RecordConsumed("test.queue", ConsumeOutcome.Duplicate);

        (long Value, Dictionary<string, object?> Tags) own;
        lock (measurements)
        {
            own = Assert.Single(measurements, measurement => Equals(measurement.Tags["queue"], "test.queue"));
        }

        Assert.Equal(1, own.Value);
        Assert.Equal(ConsumeOutcome.Duplicate, own.Tags["outcome"]);
    }
}
