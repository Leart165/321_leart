using Analytics.Infrastructure.Messaging;
using System.Diagnostics;
using System.Text;
using Xunit;

namespace Analytics.Tests.Observability;

// Der Trace der Anfrage läuft im Kopf traceparent über den eigenen Broker und wird im Konsumenten
// fortgesetzt.
public sealed class MessagingTracingTests : IDisposable
{
    private const string SenderTraceParent = "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01";

    private readonly ActivityListener _listener = new ActivityListener
    {
        ShouldListenTo = source => source.Name == MessagingTracing.SourceName,
        Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded
    };

    public MessagingTracingTests()
    {
        ActivitySource.AddActivityListener(_listener);
    }

    public void Dispose()
    {
        _listener.Dispose();
    }

    [Fact]
    public void Processing_continues_the_trace_of_the_sender()
    {
        Dictionary<string, object?> headers = new Dictionary<string, object?>
        {
            [MessagingTracing.TraceParentHeader] = Encoding.UTF8.GetBytes(SenderTraceParent)
        };

        using Activity? activity = MessagingTracing.StartProcess(
            ReportTopology.ReportsQueue, ReportTopology.ReportRequestedRoutingKey, Guid.NewGuid().ToString(), "korrelation-1", headers);

        Assert.NotNull(activity);
        Assert.Equal(ActivityKind.Consumer, activity.Kind);
        Assert.Equal("4bf92f3577b34da6a3ce929d0e0e4736", activity.TraceId.ToHexString());
        Assert.Equal("00f067aa0ba902b7", activity.ParentSpanId.ToHexString());
        Assert.Equal($"process {ReportTopology.ReportsQueue}", activity.DisplayName);
    }

    // Ohne traceparent beginnt die Verarbeitung einen eigenen Trace und nennt die correlation-id.
    [Fact]
    public void A_message_without_traceparent_begins_its_own_trace_with_the_correlation_id()
    {
        Dictionary<string, object?> headers = new Dictionary<string, object?>
        {
            ["correlation-id"] = Encoding.UTF8.GetBytes("eigene-korrelation")
        };

        using Activity? activity = MessagingTracing.StartProcess(
            ReportTopology.ReportsQueue, ReportTopology.ReportRequestedRoutingKey, Guid.NewGuid().ToString(), "eigene-korrelation", headers);

        Assert.NotNull(activity);
        Assert.Equal(default, activity.ParentSpanId);
        Assert.Equal("eigene-korrelation", activity.GetTagItem("messaging.message.conversation_id"));
        Assert.Equal(ReportTopology.ReportsQueue, activity.GetTagItem("messaging.destination.name"));
    }

    [Theory]
    [InlineData(ConsumeOutcome.DeadLettered, ActivityStatusCode.Error)]
    [InlineData(ConsumeOutcome.Retried, ActivityStatusCode.Error)]
    [InlineData(ConsumeOutcome.Duplicate, ActivityStatusCode.Unset)]
    [InlineData(ConsumeOutcome.Processed, ActivityStatusCode.Unset)]
    public void The_outcome_is_recorded_on_the_span(string outcome, ActivityStatusCode status)
    {
        using Activity? activity = MessagingTracing.StartProcess(
            ReportTopology.ReportsQueue, ReportTopology.ReportRequestedRoutingKey, null, null, headers: null);

        MessagingTracing.Complete(activity, outcome);

        Assert.Equal(status, activity!.Status);
        Assert.Equal(outcome, activity.GetTagItem("analytics.messaging.outcome"));
    }
}
