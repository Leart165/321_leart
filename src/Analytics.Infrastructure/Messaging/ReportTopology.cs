using RabbitMQ.Client;

namespace Analytics.Infrastructure.Messaging;

// Der eigene Exchange der Analytics-Firma am eigenen Broker analytics-rabbitmq, aufgebaut wie
// bank.events bei der Bank: ein Topic-Exchange für die eigenen Ereignisse, je Queue eine
// Dead-Letter-Queue über den Dead-Letter-Exchange analytics.dlx. Alle Namen beginnen mit
// analytics. Kontrakt: contracts/analytics/events.asyncapi.v1.yaml.
public static class ReportTopology
{
    public const string Exchange = "analytics.events";

    public const string ContentType = "application/json";

    // Monatsbericht.
    public const string ReportRequestedRoutingKey = "report.requested";

    public const string ReportRequestedType = "ReportRequested";

    public const string ReportsQueue = "analytics.reports";

    public const string ReportsDeadLetterQueue = "analytics.reports.dlq";

    // Jahresauszug: eigene Queue, damit ein grosser Auszug die Monatsberichte nicht aufhält.
    public const string AnnualReportRequestedRoutingKey = "annual-report.requested";

    public const string AnnualReportRequestedType = "AnnualReportRequested";

    public const string AnnualReportsQueue = "analytics.annual-reports";

    public const string AnnualReportsDeadLetterQueue = "analytics.annual-reports.dlq";

    // Idempotent: wer zuerst startet, legt an, alle anderen bestätigen nur.
    public static Task DeclareExchangeAsync(IChannel channel, CancellationToken cancellationToken)
    {
        return channel.ExchangeDeclareAsync(
            Exchange, ExchangeType.Topic, durable: true, autoDelete: false,
            cancellationToken: cancellationToken);
    }

    public static Task DeclareReportsQueueAsync(IChannel channel, CancellationToken cancellationToken)
    {
        return DeclareQueueAsync(channel, ReportsQueue, ReportsDeadLetterQueue, ReportRequestedRoutingKey, cancellationToken);
    }

    public static Task DeclareAnnualReportsQueueAsync(IChannel channel, CancellationToken cancellationToken)
    {
        return DeclareQueueAsync(
            channel, AnnualReportsQueue, AnnualReportsDeadLetterQueue, AnnualReportRequestedRoutingKey, cancellationToken);
    }

    // Eine Queue an analytics.events mit eigener Dead-Letter-Queue. Routing Key ins
    // Dead-Letter-Exchange ist der Name der Queue, wie bei der Bank: jede verworfene Nachricht
    // findet ihre eigene Dead-Letter-Queue.
    public static async Task DeclareQueueAsync(
        IChannel channel,
        string queue,
        string deadLetterQueue,
        string routingKey,
        CancellationToken cancellationToken)
    {
        await DeclareExchangeAsync(channel, cancellationToken);

        await channel.ExchangeDeclareAsync(
            MessagingTopology.DeadLetterExchange, ExchangeType.Direct, durable: true, autoDelete: false,
            cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            deadLetterQueue, durable: true, exclusive: false, autoDelete: false,
            cancellationToken: cancellationToken);

        await channel.QueueBindAsync(
            deadLetterQueue, MessagingTopology.DeadLetterExchange, queue,
            cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            queue, durable: true, exclusive: false, autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-dead-letter-exchange"] = MessagingTopology.DeadLetterExchange,
                ["x-dead-letter-routing-key"] = queue
            },
            cancellationToken: cancellationToken);

        await channel.QueueBindAsync(queue, Exchange, routingKey, cancellationToken: cancellationToken);
    }
}
