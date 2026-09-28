using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace Analytics.Infrastructure.Messaging;

// Namen am Broker, wie in contracts/analytics/asyncapi.v1.yaml (alte Queue) und
// asyncapi.v2.yaml (Partner-Queue). Der Broker-Benutzer analytics darf nur analytics.* anlegen
// und beschreiben; bank.events gehört der Bank und wird deshalb nur passiv deklariert.
public static class MessagingTopology
{
    public const string Exchange = "bank.events";

    public const string DeadLetterExchange = "analytics.dlx";
    public const string PartnerQueue = "analytics.partner";
    public const string PartnerDeadLetterQueue = "analytics.partner.dlq";
    public const string PartnerRoutingKey = "partner.transaction.completed";

    // Version 1, bis zum Contract-Schritt: angelegt noch als guest, mit bank.dlx als
    // Dead-Letter-Exchange. Queue-Argumente lassen sich nicht ändern, deshalb nur passiv.
    public const string LegacyQueue = "analytics.ledger";
    public const string LegacyDeadLetterQueue = "analytics.ledger.dlq";
    public const string LegacyDeadLetterExchange = "bank.dlx";
    public const string LegacyRoutingKey = "transaction.completed";

    public static async Task DeclarePartnerQueueAsync(IChannel channel, CancellationToken cancellationToken)
    {
        await channel.ExchangeDeclarePassiveAsync(Exchange, cancellationToken);

        await channel.ExchangeDeclareAsync(
            DeadLetterExchange, ExchangeType.Direct, durable: true, autoDelete: false,
            cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            PartnerDeadLetterQueue, durable: true, exclusive: false, autoDelete: false,
            cancellationToken: cancellationToken);

        await channel.QueueBindAsync(
            PartnerDeadLetterQueue, DeadLetterExchange, PartnerQueue,
            cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            PartnerQueue, durable: true, exclusive: false, autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-dead-letter-exchange"] = DeadLetterExchange,
                ["x-dead-letter-routing-key"] = PartnerQueue
            },
            cancellationToken: cancellationToken);

        await channel.QueueBindAsync(
            PartnerQueue, Exchange, PartnerRoutingKey,
            cancellationToken: cancellationToken);
    }

    // Gibt die Anzahl wartender Nachrichten zurück, oder null, wenn es die Queue nicht gibt.
    // Eine passive Deklaration einer fehlenden Queue schliesst den Kanal, deshalb ein eigener.
    public static async Task<uint?> CountIfExistsAsync(IConnection connection, string queue, CancellationToken cancellationToken)
    {
        await using IChannel probe = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
        try
        {
            QueueDeclareOk declared = await probe.QueueDeclarePassiveAsync(queue, cancellationToken);
            return declared.MessageCount;
        }
        catch (OperationInterruptedException exception) when (exception.ShutdownReason?.ReplyCode == Constants.NotFound)
        {
            return null;
        }
    }
}
