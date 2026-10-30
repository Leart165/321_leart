using Analytics.Application.Reports;
using Analytics.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace Analytics.Infrastructure.Messaging;

// Verarbeitet einen Antrag auf einen Bericht, egal ob Monatsbericht aus analytics.reports oder
// Jahresauszug aus analytics.annual-reports: beide Nachrichten tragen die reportId, alles andere
// steht in der eigenen Datenbank.
public sealed class ReportRequestedHandler
{
    private static readonly JsonSerializerOptions SerializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);

    private readonly ReportService _reports;
    private readonly AsyncApiSchemas _schemas;
    private readonly ILogger<ReportRequestedHandler> _logger;

    public ReportRequestedHandler(ReportService reports, AsyncApiSchemas schemas, ILogger<ReportRequestedHandler> logger)
    {
        _reports = reports;
        _schemas = schemas;
        _logger = logger;
    }

    // expectedType ist das Ereignis, das in diese Queue gehört. Trägt die Nachricht einen anderen
    // Typ, passt sie nicht hierher und geht in die Dead-Letter-Queue.
    public async Task<string> HandleAsync(
        ReadOnlyMemory<byte> body,
        string? messageType,
        string expectedType,
        bool redelivered,
        CancellationToken cancellationToken)
    {
        string payload = Encoding.UTF8.GetString(body.Span);

        if (messageType is not null && !string.Equals(messageType, expectedType, StringComparison.Ordinal))
        {
            _logger.LogError(
                "Nachricht vom Typ {Type} gehört nicht in diese Queue ({Expected}), sie geht in die Dead-Letter-Queue.",
                messageType, expectedType);
            return ConsumeOutcome.DeadLettered;
        }

        IReadOnlyList<string> errors = _schemas.Validate(expectedType, payload);
        if (errors.Count > 0)
        {
            _logger.LogError(
                "Antrag passt nicht zum Kontrakt ({Errors}), er geht in die Dead-Letter-Queue.",
                string.Join("; ", errors));
            return ConsumeOutcome.DeadLettered;
        }

        try
        {
            ReportReference request = JsonSerializer.Deserialize<ReportReference>(payload, SerializerOptions)
                ?? throw new JsonException("Der Nachrichtenkörper ist leer.");

            ReportGeneration result = await _reports.GenerateAsync(request.ReportId, cancellationToken);
            switch (result)
            {
                case ReportGeneration.Generated:
                    _logger.LogInformation(
                        "Bericht {ReportId} für {Year} als PDF erstellt ({Type}).",
                        request.ReportId, request.Year, expectedType);
                    return ConsumeOutcome.Processed;

                case ReportGeneration.Failed:
                    _logger.LogWarning("Bericht {ReportId} ist gescheitert, Grund steht im Bericht.", request.ReportId);
                    return ConsumeOutcome.Processed;

                case ReportGeneration.AlreadyCompleted:
                    _logger.LogInformation("Bericht {ReportId} war schon erstellt, nichts geändert.", request.ReportId);
                    return ConsumeOutcome.Duplicate;

                default:
                    _logger.LogError("Bericht {ReportId} gibt es nicht, der Antrag geht in die Dead-Letter-Queue.", request.ReportId);
                    return ConsumeOutcome.DeadLettered;
            }
        }
        catch (JsonException exception)
        {
            _logger.LogError("Antrag ist nicht verwertbar ({Error}), er geht in die Dead-Letter-Queue.", exception.Message);
            return ConsumeOutcome.DeadLettered;
        }
        catch (Exception exception) when (DatabaseFailures.IsTransient(exception))
        {
            _logger.LogWarning(
                "Datenbank vorübergehend nicht erreichbar ({Error}), der Antrag kommt zurück in die Queue.",
                exception.GetBaseException().Message);
            return ConsumeOutcome.Retried;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            if (redelivered)
            {
                _logger.LogError(exception, "Antrag scheitert erneut, er geht in die Dead-Letter-Queue.");
                return ConsumeOutcome.DeadLettered;
            }

            _logger.LogWarning(exception, "Antrag fehlgeschlagen, er kommt für einen zweiten Versuch zurück in die Queue.");
            return ConsumeOutcome.Retried;
        }
    }
}

// Was beide Anträge gemeinsam haben; das Schema hat die Nachricht schon geprüft.
internal sealed record ReportReference(Guid ReportId, int Year);
