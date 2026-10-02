using System.Diagnostics;
using System.Text;

namespace Analytics.Infrastructure.Messaging;

// W3C Trace Context über den Broker, wie ihn die Bank im Kopf traceparent mitschickt. Nur
// System.Diagnostics: OpenTelemetry liest die Spannen mit, sobald es eingeschaltet ist.
public static class MessagingTracing
{
    public const string SourceName = "Analytics.Messaging";

    public const string TraceParentHeader = "traceparent";

    public const string TraceStateHeader = "tracestate";

    private static readonly ActivitySource Source = new ActivitySource(SourceName);

    public static Activity? StartProcess(
        string queue,
        string routingKey,
        string? messageId,
        string? correlationId,
        IDictionary<string, object?>? headers)
    {
        ActivityContext parent = ExtractParent(headers);

        Activity? activity = Source.StartActivity($"process {queue}", ActivityKind.Consumer, parent);
        activity?.SetTag("messaging.system", "rabbitmq");
        activity?.SetTag("messaging.operation.name", "process");
        activity?.SetTag("messaging.operation.type", "process");
        activity?.SetTag("messaging.destination.name", queue);
        activity?.SetTag("messaging.rabbitmq.destination.routing_key", routingKey);
        activity?.SetTag("messaging.message.id", messageId);
        activity?.SetTag("messaging.message.conversation_id", correlationId);
        return activity;
    }

    // Spanne beim Publizieren aus der Outbox. Sie hängt am Trace, in dem das Ereignis entstand,
    // nicht am Dispatcher.
    public static Activity? StartPublish(string exchange, string routingKey, Guid messageId, string? traceParent)
    {
        ActivityContext parent = default;
        if (traceParent is not null)
        {
            ActivityContext.TryParse(traceParent, null, isRemote: false, out parent);
        }

        Activity? activity = Source.StartActivity($"publish {exchange}", ActivityKind.Producer, parent);
        activity?.SetTag("messaging.system", "rabbitmq");
        activity?.SetTag("messaging.operation.name", "publish");
        activity?.SetTag("messaging.operation.type", "send");
        activity?.SetTag("messaging.destination.name", exchange);
        activity?.SetTag("messaging.rabbitmq.destination.routing_key", routingKey);
        activity?.SetTag("messaging.message.id", messageId.ToString());
        return activity;
    }

    // Setzt traceparent in den Kopf, damit der Konsument den Trace fortsetzt.
    public static void Inject(IDictionary<string, object?> headers, Activity? activity, string? fallback)
    {
        string? traceParent = activity?.Id ?? fallback;
        if (traceParent is not null)
        {
            headers[TraceParentHeader] = traceParent;
        }
    }

    public static void Complete(Activity? activity, string outcome)
    {
        if (activity is null)
        {
            return;
        }

        activity.SetTag("analytics.messaging.outcome", outcome);
        if (outcome is ConsumeOutcome.DeadLettered or ConsumeOutcome.Retried)
        {
            activity.SetStatus(ActivityStatusCode.Error, outcome);
        }
    }

    private static ActivityContext ExtractParent(IDictionary<string, object?>? headers)
    {
        string? traceParent = HeaderText(headers, TraceParentHeader);
        if (traceParent is null)
        {
            return default;
        }

        string? traceState = HeaderText(headers, TraceStateHeader);
        return ActivityContext.TryParse(traceParent, traceState, isRemote: true, out ActivityContext context)
            ? context
            : default;
    }

    public static string? HeaderText(IDictionary<string, object?>? headers, string name)
    {
        if (headers is null || !headers.TryGetValue(name, out object? value))
        {
            return null;
        }

        return value switch
        {
            byte[] bytes => Encoding.UTF8.GetString(bytes),
            string text => text,
            _ => null
        };
    }
}
