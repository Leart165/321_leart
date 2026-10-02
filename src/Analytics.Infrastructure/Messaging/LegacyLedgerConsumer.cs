using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Analytics.Infrastructure.Messaging;

// Die alte Queue analytics.ledger mit transaction.completed, nach contracts/analytics/asyncapi.v1.yaml.
// Sie wird nie mehr angelegt, nur noch gelesen, bis sie weg ist:
//   Drain  (Expand)   weiter lesen, neben analytics.partner; jede Buchung zählt trotzdem einmal.
//   Retire (Contract) Bindung lösen, den Rest lesen, dann Queue und Dead-Letter-Queue löschen.
public sealed class LegacyLedgerConsumer : QueueConsumer
{
    private static readonly TimeSpan EmptyCheckInterval = TimeSpan.FromSeconds(5);

    public LegacyLedgerConsumer(
        IServiceScopeFactory scopes,
        RabbitMqConnection connection,
        IOptions<MessagingOptions> options,
        ILogger<LegacyLedgerConsumer> logger)
        : base(scopes, connection, options, logger)
    {
    }

    protected override string Queue
    {
        get { return MessagingTopology.LegacyQueue; }
    }

    protected override Task<string> HandleAsync(IServiceProvider services, BasicDeliverEventArgs delivery)
    {
        TransactionCompletedHandler handler = services.GetRequiredService<TransactionCompletedHandler>();
        return handler.HandleAsync(
            delivery.Body, BookingFormat.Internal, delivery.BasicProperties.Type, delivery.Redelivered, delivery.CancellationToken);
    }

    protected override async Task<bool> PrepareAsync(IConnection connection, IChannel channel, CancellationToken cancellationToken)
    {
        uint? waiting = await MessagingTopology.CountIfExistsAsync(connection, Queue, cancellationToken);
        if (waiting is null)
        {
            Logger.LogInformation(
                "[{Instance}] Die alte Queue {Queue} gibt es nicht, gelesen wird nur {PartnerQueue}.",
                Instance, Queue, MessagingTopology.PartnerQueue);

            // Die Queue ist schon weg, ihre Dead-Letter-Queue vielleicht nicht: sie blieb stehen,
            // weil noch etwas darin lag, und wurde seither angesehen und geleert.
            if (Options.LegacyQueue == LegacyQueueMode.Retire)
            {
                await RetireDeadLetterQueueAsync(connection, cancellationToken);
            }

            return false;
        }

        if (Options.LegacyQueue == LegacyQueueMode.Retire)
        {
            await channel.QueueUnbindAsync(
                Queue, MessagingTopology.Exchange, MessagingTopology.LegacyRoutingKey,
                cancellationToken: cancellationToken);

            Logger.LogInformation(
                "[{Instance}] Contract: Bindung von {Queue} an {RoutingKey} gelöst, {Waiting} Nachrichten werden noch gelesen.",
                Instance, Queue, MessagingTopology.LegacyRoutingKey, waiting);
        }
        else
        {
            Logger.LogInformation(
                "[{Instance}] Expand: die alte Queue {Queue} wird neben {PartnerQueue} weiter gelesen, {Waiting} Nachrichten warten.",
                Instance, Queue, MessagingTopology.PartnerQueue, waiting);
        }

        return true;
    }

    protected override async Task WhileConsumingAsync(IConnection connection, CancellationToken cancellationToken)
    {
        if (Options.LegacyQueue != LegacyQueueMode.Retire)
        {
            await base.WhileConsumingAsync(connection, cancellationToken);
            return;
        }

        while (true)
        {
            await Task.Delay(EmptyCheckInterval, cancellationToken);

            uint? waiting = await MessagingTopology.CountIfExistsAsync(connection, Queue, cancellationToken);
            if (waiting is null || (waiting == 0 && InFlightCount == 0))
            {
                return;
            }
        }
    }

    // Erst nach dem Abmelden löschen: sonst bräche der Broker dem eigenen Konsumenten die Queue weg.
    protected override async Task AfterConsumingAsync(IConnection connection, CancellationToken cancellationToken)
    {
        if (Options.LegacyQueue != LegacyQueueMode.Retire)
        {
            return;
        }

        await DeleteIfEmptyAsync(connection, Queue, cancellationToken);
        await RetireDeadLetterQueueAsync(connection, cancellationToken);
    }

    private async Task RetireDeadLetterQueueAsync(IConnection connection, CancellationToken cancellationToken)
    {
        uint? deadLetters = await MessagingTopology.CountIfExistsAsync(connection, MessagingTopology.LegacyDeadLetterQueue, cancellationToken);
        if (deadLetters is null)
        {
            return;
        }

        if (deadLetters > 0)
        {
            Logger.LogWarning(
                "[{Instance}] {Queue} enthält noch {Count} Nachrichten und bleibt zum Nachsehen stehen.",
                Instance, MessagingTopology.LegacyDeadLetterQueue, deadLetters);
            return;
        }

        await DeleteIfEmptyAsync(connection, MessagingTopology.LegacyDeadLetterQueue, cancellationToken);
    }

    // ifEmpty: kommt zwischen Prüfung und Löschen doch noch etwas an, lehnt der Broker ab, statt
    // es zu verwerfen. Eine zweite Instanz, die gleichzeitig löscht, findet die Queue nicht mehr.
    private async Task DeleteIfEmptyAsync(IConnection connection, string queue, CancellationToken cancellationToken)
    {
        await using IChannel channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
        try
        {
            await channel.QueueDeleteAsync(queue, ifUnused: false, ifEmpty: true, cancellationToken: cancellationToken);
            Logger.LogInformation("[{Instance}] Contract: {Queue} ist leer und gelöscht.", Instance, queue);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Logger.LogWarning(
                "[{Instance}] {Queue} liess sich nicht löschen ({Error}), beim nächsten Start neuer Versuch.",
                Instance, queue, exception.Message);
        }
    }
}
