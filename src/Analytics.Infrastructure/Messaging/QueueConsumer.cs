using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Diagnostics;

namespace Analytics.Infrastructure.Messaging;

// Was bei jeder Queue gleich ist: Verbindung, Wiederholen bei Broker-Ausfall, Ack oder Nack je
// Ergebnis, Metrik, Trace und sauberes Herunterfahren. Die Unterklassen sagen nur, welche Queue,
// was vor dem Konsumieren am Broker vorbereitet werden muss und wer eine Nachricht verarbeitet.
public abstract class QueueConsumer : BackgroundService
{
    private const string CorrelationHeader = "correlation-id";
    private static readonly TimeSpan CancelTimeout = TimeSpan.FromSeconds(5);

    private readonly IServiceScopeFactory _scopes;
    private readonly RabbitMqConnection _connection;
    private readonly InFlightMessages _inFlight = new InFlightMessages();

    private IChannel? _channel;
    private string? _consumerTag;

    protected QueueConsumer(
        IServiceScopeFactory scopes,
        RabbitMqConnection connection,
        IOptions<MessagingOptions> options,
        ILogger logger)
    {
        _scopes = scopes;
        _connection = connection;
        Options = options.Value;
        Logger = logger;
    }

    protected MessagingOptions Options { get; }

    protected ILogger Logger { get; }

    protected string Instance { get; } = Environment.MachineName;

    protected int InFlightCount
    {
        get { return _inFlight.Count; }
    }

    protected abstract string Queue { get; }

    // Legt am Broker an oder prüft, was die Queue braucht. false heisst: nicht konsumieren.
    protected abstract Task<bool> PrepareAsync(IConnection connection, IChannel channel, CancellationToken cancellationToken);

    // Verarbeitet eine Nachricht in einem eigenen Scope und liefert ein ConsumeOutcome.
    protected abstract Task<string> HandleAsync(IServiceProvider services, BasicDeliverEventArgs delivery);

    // Läuft, solange konsumiert wird. Kehrt es zurück, ist diese Queue erledigt.
    protected virtual Task WhileConsumingAsync(IConnection connection, CancellationToken cancellationToken)
    {
        return Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
    }

    // Läuft, nachdem der Konsument abgemeldet und der Kanal geschlossen ist.
    protected virtual Task AfterConsumingAsync(IConnection connection, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                IConnection connection = await _connection.GetAsync(stoppingToken);
                if (!await StartConsumingAsync(connection, stoppingToken))
                {
                    return;
                }

                // Verbindungsabbrüche heilt die automatische Wiederherstellung von RabbitMQ.Client.
                await WhileConsumingAsync(connection, stoppingToken);
                await StopReceivingAsync(stoppingToken);
                await CloseChannelAsync();
                await AfterConsumingAsync(connection, stoppingToken);
                return;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                Logger.LogWarning(
                    "[{Instance}] Queue {Queue} auf dem Broker {Host} nicht verfügbar ({Error}), neuer Versuch in {Delay} Sekunden.",
                    Instance, Queue, _connection.Host, exception.Message, Options.RetryDelay.TotalSeconds);

                await CloseChannelAsync();
                await Task.Delay(Options.RetryDelay, stoppingToken);
            }
        }
    }

    // Beim Stoppen: erst beim Broker abmelden, dann laufende Nachrichten zu Ende verarbeiten,
    // erst dann den Kanal schliessen.
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await StopReceivingAsync(cancellationToken);
        await base.StopAsync(cancellationToken);
        await CloseChannelAsync();
    }

    private async Task<bool> StartConsumingAsync(IConnection connection, CancellationToken cancellationToken)
    {
        await CloseChannelAsync();

        IChannel channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
        _channel = channel;

        if (!await PrepareAsync(connection, channel, cancellationToken))
        {
            await CloseChannelAsync();
            return false;
        }

        // Ohne Prefetch zöge die erste Instanz die ganze Queue an sich, und Skalieren brächte nichts.
        await channel.BasicQosAsync(
            prefetchSize: 0, prefetchCount: Options.Prefetch, global: false,
            cancellationToken: cancellationToken);

        AsyncEventingBasicConsumer consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += (_, delivery) => OnReceivedAsync(channel, delivery);

        _consumerTag = await channel.BasicConsumeAsync(
            Queue, autoAck: false, consumer: consumer,
            cancellationToken: cancellationToken);

        Logger.LogInformation(
            "[{Instance}] konsumiere Queue {Queue}, Prefetch {Prefetch}.",
            Instance, Queue, Options.Prefetch);
        return true;
    }

    private async Task OnReceivedAsync(IChannel channel, BasicDeliverEventArgs delivery)
    {
        using IDisposable working = _inFlight.Begin();

        string correlationId = ReadCorrelationId(delivery);

        // Mit traceparent (transaction.completed, report.requested) setzt die Spanne den Trace des
        // Absenders fort; ohne, wie beim Partner-Ereignis, beginnt hier ein eigener.
        using Activity? activity = MessagingTracing.StartProcess(
            Queue, delivery.RoutingKey, delivery.BasicProperties.MessageId, correlationId,
            delivery.BasicProperties.Headers);

        using IDisposable? logScope = Logger.BeginScope(new Dictionary<string, object?>
        {
            ["CorrelationId"] = correlationId,
            ["MessageId"] = delivery.BasicProperties.MessageId,
            ["Queue"] = Queue
        });

        string outcome;
        using (IServiceScope scope = _scopes.CreateScope())
        {
            outcome = await HandleAsync(scope.ServiceProvider, delivery);
        }

        if (ConsumeOutcome.IsAcknowledged(outcome))
        {
            await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false);
        }
        else if (outcome == ConsumeOutcome.Retried)
        {
            // Kurz bremsen, damit eine ausgefallene Datenbank nicht im Millisekundentakt
            // gefragt wird. Ohne Token, damit das Nack auch beim Herunterfahren noch folgt.
            await Task.Delay(Options.RetryDelay, CancellationToken.None);
            await channel.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: true);
        }
        else
        {
            await channel.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: false);
        }

        MessagingMetrics.RecordConsumed(Queue, outcome);
        MessagingTracing.Complete(activity, outcome);
    }

    private async Task StopReceivingAsync(CancellationToken cancellationToken)
    {
        IChannel? channel = _channel;
        string? consumerTag = _consumerTag;
        _consumerTag = null;

        if (channel is not null && consumerTag is not null && channel.IsOpen)
        {
            try
            {
                using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                deadline.CancelAfter(CancelTimeout);
                await channel.BasicCancelAsync(consumerTag, noWait: false, deadline.Token);
            }
            catch (Exception exception)
            {
                // Ohne Broker gibt es niemanden, bei dem man sich abmelden könnte. Dann eben nur warten.
                Logger.LogDebug(exception, "[{Instance}] Abmelden von {Queue} nicht möglich.", Instance, Queue);
            }
        }

        if (_inFlight.Count > 0)
        {
            Logger.LogInformation(
                "[{Instance}] nimmt von {Queue} nichts mehr an und wartet auf {Running} laufende Nachrichten.",
                Instance, Queue, _inFlight.Count);
        }

        try
        {
            await _inFlight.WaitUntilIdleAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            Logger.LogWarning(
                "[{Instance}] Frist beim Herunterfahren abgelaufen, {Running} Nachrichten von {Queue} gehen zurück an den Broker.",
                Instance, _inFlight.Count, Queue);
        }
    }

    // Schliesst und entsorgt den Kanal auch nach einem Fehler, damit keine Kanäle liegen bleiben:
    // der Broker erlaubt dem Benutzer analytics höchstens 50.
    private async Task CloseChannelAsync()
    {
        IChannel? channel = _channel;
        _channel = null;
        if (channel is null)
        {
            return;
        }

        try
        {
            if (channel.IsOpen)
            {
                await channel.CloseAsync();
            }
        }
        catch (Exception exception)
        {
            Logger.LogDebug(exception, "[{Instance}] Kanal zu {Queue} liess sich nicht sauber schliessen.", Instance, Queue);
        }
        finally
        {
            await channel.DisposeAsync();
        }
    }

    private static string ReadCorrelationId(BasicDeliverEventArgs delivery)
    {
        if (!string.IsNullOrWhiteSpace(delivery.BasicProperties.CorrelationId))
        {
            return delivery.BasicProperties.CorrelationId;
        }

        return MessagingTracing.HeaderText(delivery.BasicProperties.Headers, CorrelationHeader)
            ?? Guid.NewGuid().ToString();
    }
}
