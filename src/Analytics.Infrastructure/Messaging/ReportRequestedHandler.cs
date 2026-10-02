using Analytics.Application.Reports;
using Analytics.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace Analytics.Infrastructure.Messaging;

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

    public async Task<string> HandleAsync(
        ReadOnlyMemory<byte> body,
        string? messageType,
        bool redelivered,
        CancellationToken cancellationToken)
    {
        string payload = Encoding.UTF8.GetString(body.Span);

        IReadOnlyList<string> errors = _schemas.Validate(messageType ?? ReportTopology.ReportRequestedType, payload);
        if (errors.Count > 0)
        {
            _logger.LogError(
                "Antrag passt nicht zum Kontrakt ({Errors}), er geht in die Dead-Letter-Queue.",
                string.Join("; ", errors));
            return ConsumeOutcome.DeadLettered;
        }

        try
        {
            ReportRequestedPayload request = JsonSerializer.Deserialize<ReportRequestedPayload>(payload, SerializerOptions)
                ?? throw new JsonException("Der Nachrichtenkörper ist leer.");

            ReportGeneration result = await _reports.GenerateAsync(request.ReportId, cancellationToken);
            switch (result)
            {
                case ReportGeneration.Generated:
                    _logger.LogInformation(
                        "Monatsbericht {ReportId} für {Year}-{Month:D2} als PDF erstellt.",
                        request.ReportId, request.Year, request.Month);
                    return ConsumeOutcome.Processed;

                case ReportGeneration.Failed:
                    _logger.LogWarning("Monatsbericht {ReportId} ist gescheitert, Grund steht im Bericht.", request.ReportId);
                    return ConsumeOutcome.Processed;

                case ReportGeneration.AlreadyCompleted:
                    _logger.LogInformation("Monatsbericht {ReportId} war schon erstellt, nichts geändert.", request.ReportId);
                    return ConsumeOutcome.Duplicate;

                default:
                    _logger.LogError("Monatsbericht {ReportId} gibt es nicht, der Antrag geht in die Dead-Letter-Queue.", request.ReportId);
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
