using RabbitMQ.Client;

namespace Analytics.Infrastructure.Messaging;

// Der eigene Exchange der Analytics-Firma, aufgebaut wie bank.events: ein Topic-Exchange für die
// eigenen Ereignisse, je Queue eine Dead-Letter-Queue über den Dead-Letter-Exchange analytics.dlx.
// Der Broker-Benutzer analytics darf alles mit dem Präfix analytics. anlegen und beschreiben.
// Kontrakt: contracts/analytics/events.asyncapi.v1.yaml.
public static class ReportTopology
{
    public const string Exchange = "analytics.events";

    public const string ReportRequestedRoutingKey = "report.requested";

    public const string ReportRequestedType = "ReportRequested";

    public const string ReportsQueue = "analytics.reports";

    public const string ReportsDeadLetterQueue = "analytics.reports.dlq";

    public const string ContentType = "application/json";

    // Idempotent: wer zuerst startet, legt an, alle anderen bestätigen nur.
    public static Task DeclareExchangeAsync(IChannel channel, CancellationToken cancellationToken)
    {
        return channel.ExchangeDeclareAsync(
            Exchange, ExchangeType.Topic, durable: true, autoDelete: false,
            cancellationToken: cancellationToken);
    }

    public static async Task DeclareReportsQueueAsync(IChannel channel, CancellationToken cancellationToken)
    {
        await DeclareExchangeAsync(channel, cancellationToken);

        await channel.ExchangeDeclareAsync(
            MessagingTopology.DeadLetterExchange, ExchangeType.Direct, durable: true, autoDelete: false,
            cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            ReportsDeadLetterQueue, durable: true, exclusive: false, autoDelete: false,
            cancellationToken: cancellationToken);

        // Routing Key ist der Name der Queue, wie bei der Bank: jede verworfene Nachricht findet
        // ihre eigene Dead-Letter-Queue.
        await channel.QueueBindAsync(
            ReportsDeadLetterQueue, MessagingTopology.DeadLetterExchange, ReportsQueue,
            cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            ReportsQueue, durable: true, exclusive: false, autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-dead-letter-exchange"] = MessagingTopology.DeadLetterExchange,
                ["x-dead-letter-routing-key"] = ReportsQueue
            },
            cancellationToken: cancellationToken);

        await channel.QueueBindAsync(
            ReportsQueue, Exchange, ReportRequestedRoutingKey,
            cancellationToken: cancellationToken);
    }
}
