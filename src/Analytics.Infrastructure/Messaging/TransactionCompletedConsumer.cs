using Analytics.Application.Ledger;
using Analytics.Domain.Exceptions;
using Analytics.Domain.Ledger;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace Analytics.Infrastructure.Messaging;

public sealed class TransactionCompletedConsumer : BackgroundService
{
    private static readonly JsonSerializerOptions SerializerOptions = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IServiceScopeFactory _scopes;
    private readonly RabbitMqConnection _connection;
    private readonly MessagingOptions _options;
    private readonly ILogger<TransactionCompletedConsumer> _logger;

    public TransactionCompletedConsumer(
        IServiceScopeFactory scopes,
        RabbitMqConnection connection,
        IOptions<MessagingOptions> options,
        ILogger<TransactionCompletedConsumer> logger)
    {
        _scopes = scopes;
        _connection = connection;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ConsumeAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    "Der Broker ist nicht erreichbar ({Error}), neuer Versuch in {Delay} Sekunden.",
                    exception.Message, _options.RetryDelay.TotalSeconds);

                await Task.Delay(_options.RetryDelay, stoppingToken);
            }
        }
    }

    private async Task ConsumeAsync(CancellationToken stoppingToken)
    {
        IConnection connection = await _connection.GetAsync(stoppingToken);
        await using IChannel channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

        await MessagingTopology.DeclareAsync(channel, stoppingToken);

        await channel.BasicQosAsync(
            prefetchSize: 0, prefetchCount: _options.Prefetch, global: false,
            cancellationToken: stoppingToken);

        AsyncEventingBasicConsumer consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += (sender, delivery) => HandleAsync(channel, delivery, stoppingToken);

        await channel.BasicConsumeAsync(
            MessagingTopology.Queue, autoAck: false, consumer: consumer,
            cancellationToken: stoppingToken);

        _logger.LogInformation(
            "Warte auf {RoutingKey} in der Queue {Queue}.",
            MessagingTopology.RoutingKey, MessagingTopology.Queue);

        while (!stoppingToken.IsCancellationRequested && channel.IsOpen)
        {
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }

    private async Task HandleAsync(
        IChannel channel,
        BasicDeliverEventArgs delivery,
        CancellationToken cancellationToken)
    {
        string correlationId = ReadCorrelationId(delivery);

        using IDisposable? logScope = _logger.BeginScope(
            new Dictionary<string, object> { ["CorrelationId"] = correlationId });

        try
        {
            TransactionCompletedPayload payload =
                JsonSerializer.Deserialize<TransactionCompletedPayload>(delivery.Body.Span, SerializerOptions)
                ?? throw new InvalidLedgerEventException("Der Nachrichtenkörper ist leer.");

            BookedTransaction transaction = payload.ToBookedTransaction();

            using IServiceScope scope = _scopes.CreateScope();
            LedgerProjection projection = scope.ServiceProvider.GetRequiredService<LedgerProjection>();

            bool counted = await projection.ApplyAsync(transaction, cancellationToken);

            await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, cancellationToken);

            if (counted)
            {
                _logger.LogInformation(
                    "Buchung {TransactionId} ({Kind} {Amount} {Currency}) gezählt. Korrelation {CorrelationId}.",
                    transaction.TransactionId, transaction.Kind, transaction.Amount,
                    transaction.Currency.Code, correlationId);
            }
            else
            {
                _logger.LogInformation(
                    "Buchung {TransactionId} war schon gezählt, nichts geändert. Korrelation {CorrelationId}.",
                    transaction.TransactionId, correlationId);
            }
        }
        catch (Exception exception) when (exception is JsonException or InvalidLedgerEventException)
        {
            _logger.LogError(
                "Nachricht ist nicht verwertbar ({Error}), sie geht in die Dead-Letter-Queue. Korrelation {CorrelationId}.",
                exception.Message, correlationId);

            await channel.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: false, cancellationToken);
        }
        catch (Exception exception) when (IsTransientDatabaseFailure(exception))
        {
            _logger.LogWarning(
                "Die Datenbank ist gerade nicht erreichbar ({Error}), die Nachricht kommt zurück in die Queue. Korrelation {CorrelationId}.",
                exception.Message, correlationId);

            await channel.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: true, cancellationToken);
        }
        catch (Exception exception)
        {
            bool requeue = !delivery.Redelivered;

            _logger.LogError(
                exception,
                "Nachricht konnte nicht verarbeitet werden. Erneut zustellen: {Requeue}. Korrelation {CorrelationId}.",
                requeue, correlationId);

            await channel.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue, cancellationToken);
        }
    }

    private static bool IsTransientDatabaseFailure(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is NpgsqlException npgsql && npgsql.IsTransient)
            {
                return true;
            }

            if (current is SocketException or TimeoutException)
            {
                return true;
            }
        }

        return false;
    }

    private static string ReadCorrelationId(BasicDeliverEventArgs delivery)
    {
        if (!string.IsNullOrWhiteSpace(delivery.BasicProperties.CorrelationId))
        {
            return delivery.BasicProperties.CorrelationId;
        }

        if (delivery.BasicProperties.Headers is not null
            && delivery.BasicProperties.Headers.TryGetValue("correlation-id", out object? value)
            && value is byte[] bytes)
        {
            return Encoding.UTF8.GetString(bytes);
        }

        return Guid.NewGuid().ToString();
    }
}
