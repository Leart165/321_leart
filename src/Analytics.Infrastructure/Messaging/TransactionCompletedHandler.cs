using Analytics.Application.Ledger;
using Analytics.Domain.Exceptions;
using Analytics.Domain.Ledger;
using Analytics.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace Analytics.Infrastructure.Messaging;

public sealed class TransactionCompletedHandler
{
    private static readonly JsonSerializerOptions SerializerOptions = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly LedgerProjection _projection;
    private readonly AsyncApiSchemas _schemas;
    private readonly ILogger<TransactionCompletedHandler> _logger;

    public TransactionCompletedHandler(
        LedgerProjection projection,
        AsyncApiSchemas schemas,
        ILogger<TransactionCompletedHandler> logger)
    {
        _projection = projection;
        _schemas = schemas;
        _logger = logger;
    }

    public async Task<string> HandleAsync(
        ReadOnlyMemory<byte> body,
        BookingFormat format,
        string? messageType,
        bool redelivered,
        CancellationToken cancellationToken)
    {
        string payload = Encoding.UTF8.GetString(body.Span);

        // Tolerant Reader: ohne Typ im Kopf wird nicht abgelehnt, sondern fachlich geprüft.
        if (!string.IsNullOrWhiteSpace(messageType) && _schemas.Knows(messageType))
        {
            IReadOnlyList<string> errors = _schemas.Validate(messageType, payload);
            if (errors.Count > 0)
            {
                _logger.LogError(
                    "Nachricht vom Typ {Type} passt nicht zum Kontrakt ({Errors}), sie geht in die Dead-Letter-Queue.",
                    messageType, string.Join("; ", errors));
                return ConsumeOutcome.DeadLettered;
            }
        }

        try
        {
            BookedTransaction transaction = Read(payload, format);

            bool counted = await _projection.ApplyAsync(transaction, cancellationToken);
            if (!counted)
            {
                _logger.LogInformation(
                    "Buchung {TransactionId} war schon gezählt, nichts geändert.",
                    transaction.TransactionId);
                return ConsumeOutcome.Duplicate;
            }

            _logger.LogInformation(
                "Buchung {TransactionId} ({Kind} {Amount} {Currency}) gezählt.",
                transaction.TransactionId, transaction.Kind, transaction.Amount, transaction.Currency.Code);
            return ConsumeOutcome.Processed;
        }
        catch (Exception exception) when (exception is JsonException or InvalidLedgerEventException)
        {
            _logger.LogError(
                "Nachricht ist nicht verwertbar ({Error}), sie geht in die Dead-Letter-Queue.",
                exception.Message);
            return ConsumeOutcome.DeadLettered;
        }
        catch (Exception exception) when (DatabaseFailures.IsTransient(exception))
        {
            // Die Buchung ist in Ordnung, nur die Datenbank gerade nicht. Nie in die
            // Dead-Letter-Queue, sonst ginge sie bei jedem Datenbankneustart verloren.
            _logger.LogWarning(
                "Datenbank vorübergehend nicht erreichbar ({Error}), die Nachricht kommt zurück in die Queue.",
                exception.GetBaseException().Message);
            return ConsumeOutcome.Retried;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            if (redelivered)
            {
                _logger.LogError(exception, "Nachricht scheitert erneut, sie geht in die Dead-Letter-Queue.");
                return ConsumeOutcome.DeadLettered;
            }

            _logger.LogWarning(exception, "Nachricht fehlgeschlagen, sie kommt für einen zweiten Versuch zurück in die Queue.");
            return ConsumeOutcome.Retried;
        }
    }

    private static BookedTransaction Read(string payload, BookingFormat format)
    {
        return format switch
        {
            BookingFormat.Partner =>
                (JsonSerializer.Deserialize<PartnerTransactionCompletedPayload>(payload, SerializerOptions)
                    ?? throw new InvalidLedgerEventException("Der Nachrichtenkörper ist leer.")).ToBookedTransaction(),

            _ =>
                (JsonSerializer.Deserialize<TransactionCompletedPayload>(payload, SerializerOptions)
                    ?? throw new InvalidLedgerEventException("Der Nachrichtenkörper ist leer.")).ToBookedTransaction()
        };
    }
}
