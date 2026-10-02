using Analytics.Infrastructure.Messaging;
using Analytics.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using System.Diagnostics;
using System.Text;

namespace Analytics.Infrastructure.Outbox;

// Holt die noch nicht publizierten Ereignisse aus der Outbox und gibt sie an den Exchange
// analytics.events. Gleich gebaut wie der OutboxDispatcher der Bank.
//
// Läuft in jeder Instanz mit. FOR UPDATE SKIP LOCKED gibt jede Zeile genau einer Instanz; die
// andere wartet nicht, sondern nimmt die nächste.
//
// Steht der Broker, bleiben die Zeilen liegen und werden später versucht; die API nimmt in der
// Zwischenzeit weiter Anträge an. Zugesichert ist "mindestens einmal": stirbt der Prozess
// zwischen Publizieren und Vermerk, geht dieselbe Nachricht noch einmal hinaus. Der Konsument
// verträgt das.
public sealed class OutboxDispatcher : BackgroundService
{
    private const int BatchSize = 50;
    private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan BatchGracePeriod = TimeSpan.FromSeconds(5);

    private readonly IServiceScopeFactory _scopes;
    private readonly RabbitMqConnection _connection;
    private readonly MessagingOptions _options;
    private readonly ILogger<OutboxDispatcher> _logger;

    public OutboxDispatcher(
        IServiceScopeFactory scopes,
        RabbitMqConnection connection,
        IOptions<MessagingOptions> options,
        ILogger<OutboxDispatcher> logger)
    {
        _scopes = scopes;
        _connection = connection;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Ein angefangener Stapel darf beim Herunterfahren noch fertig werden, aber nicht ewig.
        using CancellationTokenSource batchDeadline = new CancellationTokenSource();
        using CancellationTokenRegistration onStopping =
            stoppingToken.Register(() => batchDeadline.CancelAfter(BatchGracePeriod));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                int sent = await DispatchBatchAsync(batchDeadline.Token);
                if (sent == 0)
                {
                    await Task.Delay(Interval, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    "Outbox konnte nicht geleert werden ({Error}), neuer Versuch in {Delay} Sekunden.",
                    exception.Message, _options.RetryDelay.TotalSeconds);
                await Task.Delay(_options.RetryDelay, stoppingToken);
            }
        }
    }

    public async Task<int> DispatchBatchAsync(CancellationToken cancellationToken)
    {
        using IServiceScope scope = _scopes.CreateScope();
        AnalyticsDbContext context = scope.ServiceProvider.GetRequiredService<AnalyticsDbContext>();

        await using IDbContextTransaction transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        List<OutboxMessage> pending = await context.Outbox
            .FromSql($@"SELECT * FROM outbox
                        WHERE published_at IS NULL
                        ORDER BY created_at
                        LIMIT {BatchSize}
                        FOR UPDATE SKIP LOCKED")
            .ToListAsync(cancellationToken);

        if (pending.Count == 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            return 0;
        }

        IConnection connection = await _connection.GetAsync(cancellationToken);

        // Mit Publisher Confirms: BasicPublishAsync kehrt erst zurück, wenn der Broker die
        // Nachricht angenommen hat. Erst dann wird sie als publiziert vermerkt.
        CreateChannelOptions channelOptions = new CreateChannelOptions(
            publisherConfirmationsEnabled: true,
            publisherConfirmationTrackingEnabled: true);

        await using IChannel channel = await connection.CreateChannelAsync(channelOptions, cancellationToken);
        await ReportTopology.DeclareExchangeAsync(channel, cancellationToken);

        foreach (OutboxMessage message in pending)
        {
            using IDisposable? logScope = _logger.BeginScope(new Dictionary<string, object>
            {
                ["CorrelationId"] = message.CorrelationId,
                ["MessageId"] = message.Id
            });

            using Activity? activity = MessagingTracing.StartPublish(
                ReportTopology.Exchange, message.RoutingKey, message.Id, message.TraceParent);

            Dictionary<string, object?> headers = new Dictionary<string, object?>
            {
                ["schema-version"] = message.SchemaVersion,
                ["correlation-id"] = message.CorrelationId
            };
            MessagingTracing.Inject(headers, activity, message.TraceParent);

            BasicProperties properties = new BasicProperties
            {
                Persistent = true,
                ContentType = ReportTopology.ContentType,
                MessageId = message.Id.ToString(),
                CorrelationId = message.CorrelationId,
                Type = message.Type,
                Headers = headers
            };

            await channel.BasicPublishAsync(
                exchange: ReportTopology.Exchange,
                routingKey: message.RoutingKey,
                mandatory: false,
                basicProperties: properties,
                body: Encoding.UTF8.GetBytes(message.Payload),
                cancellationToken: cancellationToken);

            message.MarkPublished(DateTimeOffset.UtcNow);

            _logger.LogInformation(
                "Ereignis {RoutingKey} mit Id {MessageId} auf {Exchange} publiziert und vom Broker bestätigt.",
                message.RoutingKey, message.Id, ReportTopology.Exchange);
        }

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return pending.Count;
    }
}
