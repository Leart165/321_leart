using RabbitMQ.Client;

namespace Analytics.Infrastructure.Messaging;

public static class MessagingTopology
{
    public const string Exchange = "bank.events";
    public const string DeadLetterExchange = "bank.dlx";
    public const string Queue = "analytics.ledger";
    public const string DeadLetterQueue = "analytics.ledger.dlq";
    public const string RoutingKey = "transaction.completed";

    public static async Task DeclareAsync(IChannel channel, CancellationToken cancellationToken)
    {
        await channel.ExchangeDeclareAsync(
            Exchange, ExchangeType.Topic, durable: true, autoDelete: false,
            cancellationToken: cancellationToken);

        await channel.ExchangeDeclareAsync(
            DeadLetterExchange, ExchangeType.Direct, durable: true, autoDelete: false,
            cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            Queue, durable: true, exclusive: false, autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-dead-letter-exchange"] = DeadLetterExchange,
                ["x-dead-letter-routing-key"] = Queue
            },
            cancellationToken: cancellationToken);

        await channel.QueueBindAsync(
            Queue, Exchange, RoutingKey,
            cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            DeadLetterQueue, durable: true, exclusive: false, autoDelete: false,
            cancellationToken: cancellationToken);

        await channel.QueueBindAsync(
            DeadLetterQueue, DeadLetterExchange, Queue,
            cancellationToken: cancellationToken);
    }
}
