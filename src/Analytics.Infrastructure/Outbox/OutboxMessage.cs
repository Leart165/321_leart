namespace Analytics.Infrastructure.Outbox;

// Ein Ereignis, das noch an den Broker muss. Entsteht in derselben Transaktion wie die Änderung,
// die es meldet, und wird danach vom OutboxDispatcher publiziert. Wie die Outbox der Bank.
public sealed class OutboxMessage
{
    // Nur für EF Core.
    private OutboxMessage()
    {
        RoutingKey = string.Empty;
        Type = string.Empty;
        CorrelationId = string.Empty;
        Payload = string.Empty;
    }

    private OutboxMessage(
        Guid id,
        string routingKey,
        string type,
        int schemaVersion,
        string correlationId,
        string? traceParent,
        string payload,
        DateTimeOffset createdAt)
    {
        Id = id;
        RoutingKey = routingKey;
        Type = type;
        SchemaVersion = schemaVersion;
        CorrelationId = correlationId;
        TraceParent = traceParent;
        Payload = payload;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public string RoutingKey { get; private set; }

    public string Type { get; private set; }

    public int SchemaVersion { get; private set; }

    public string CorrelationId { get; private set; }

    // Der Trace, in dem das Ereignis entstand. Der Dispatcher hängt seine Spanne daran, und der
    // Konsument setzt ihn fort: so steht der ganze Weg in einem Trace.
    public string? TraceParent { get; private set; }

    public string Payload { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? PublishedAt { get; private set; }

    public static OutboxMessage Create(
        string routingKey,
        string type,
        int schemaVersion,
        string correlationId,
        string? traceParent,
        string payload,
        DateTimeOffset createdAt)
    {
        return new OutboxMessage(Guid.NewGuid(), routingKey, type, schemaVersion, correlationId, traceParent, payload, createdAt);
    }

    public void MarkPublished(DateTimeOffset publishedAt)
    {
        PublishedAt = publishedAt;
    }
}
