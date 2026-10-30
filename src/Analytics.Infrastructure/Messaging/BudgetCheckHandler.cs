using Analytics.Application.Budgets;
using Analytics.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace Analytics.Infrastructure.Messaging;

// Prüft die Ausgaben eines Monats gegen das Budget und legt bei 80 und bei 100 Prozent je eine
// Warnung an. Gleiche Fehlerbehandlung wie bei den Berichten.
public sealed class BudgetCheckHandler
{
    private static readonly JsonSerializerOptions SerializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);

    private readonly BudgetService _budgets;
    private readonly AsyncApiSchemas _schemas;
    private readonly ILogger<BudgetCheckHandler> _logger;

    public BudgetCheckHandler(BudgetService budgets, AsyncApiSchemas schemas, ILogger<BudgetCheckHandler> logger)
    {
        _budgets = budgets;
        _schemas = schemas;
        _logger = logger;
    }

    public async Task<string> HandleAsync(ReadOnlyMemory<byte> body, bool redelivered, CancellationToken cancellationToken)
    {
        string payload = Encoding.UTF8.GetString(body.Span);

        IReadOnlyList<string> errors = _schemas.Validate(BudgetTopology.CheckRequestedType, payload);
        if (errors.Count > 0)
        {
            _logger.LogError(
                "Budget-Prüfung passt nicht zum Kontrakt ({Errors}), sie geht in die Dead-Letter-Queue.",
                string.Join("; ", errors));
            return ConsumeOutcome.DeadLettered;
        }

        try
        {
            BudgetCheckRequestedPayload request = JsonSerializer.Deserialize<BudgetCheckRequestedPayload>(payload, SerializerOptions)
                ?? throw new JsonException("Der Nachrichtenkörper ist leer.");

            BudgetCheck result = await _budgets.CheckAsync(request.OwnerId, request.Year, request.Month, cancellationToken);
            switch (result)
            {
                case BudgetCheck.Raised:
                    _logger.LogWarning(
                        "Budget von {Owner} für {Year}-{Month:D2} erreicht eine neue Stufe, Warnung angelegt.",
                        request.OwnerId, request.Year, request.Month);
                    return ConsumeOutcome.Processed;

                case BudgetCheck.Unchanged:
                    _logger.LogInformation(
                        "Budget von {Owner} für {Year}-{Month:D2} geprüft, keine neue Warnung.",
                        request.OwnerId, request.Year, request.Month);
                    return ConsumeOutcome.Duplicate;

                default:
                    _logger.LogInformation("{Owner} hat kein Budget mehr, nichts zu prüfen.", request.OwnerId);
                    return ConsumeOutcome.Processed;
            }
        }
        catch (JsonException exception)
        {
            _logger.LogError("Budget-Prüfung ist nicht verwertbar ({Error}), sie geht in die Dead-Letter-Queue.", exception.Message);
            return ConsumeOutcome.DeadLettered;
        }
        catch (Exception exception) when (DatabaseFailures.IsTransient(exception))
        {
            _logger.LogWarning(
                "Datenbank vorübergehend nicht erreichbar ({Error}), die Prüfung kommt zurück in die Queue.",
                exception.GetBaseException().Message);
            return ConsumeOutcome.Retried;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            if (redelivered)
            {
                _logger.LogError(exception, "Budget-Prüfung scheitert erneut, sie geht in die Dead-Letter-Queue.");
                return ConsumeOutcome.DeadLettered;
            }

            _logger.LogWarning(exception, "Budget-Prüfung fehlgeschlagen, sie kommt für einen zweiten Versuch zurück in die Queue.");
            return ConsumeOutcome.Retried;
        }
    }
}
