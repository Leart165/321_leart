using Analytics.Infrastructure.Persistence;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Analytics.Api.Middleware;

// Ist die eigene Datenbank weg, ist das ein vorübergehender Zustand und kein Fehler des Aufrufers:
// 503 mit Retry-After statt 500, damit er weiss, dass sich ein neuer Versuch lohnt.
public sealed class DatabaseUnavailableHandler : IExceptionHandler
{
    private const string RetryAfterSeconds = "2";

    private readonly IProblemDetailsService _problemDetails;
    private readonly ILogger<DatabaseUnavailableHandler> _logger;

    public DatabaseUnavailableHandler(IProblemDetailsService problemDetails, ILogger<DatabaseUnavailableHandler> logger)
    {
        _problemDetails = problemDetails;
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        if (!DatabaseFailures.IsTransient(exception) || context.Response.HasStarted)
        {
            return false;
        }

        string correlationId = CorrelationId.Of(context);
        _logger.LogWarning(
            "Anfrage {Method} {Path} endet mit 503, die Datenbank ist nicht erreichbar ({Error}). Korrelation {CorrelationId}.",
            context.Request.Method, context.Request.Path, exception.GetBaseException().Message, correlationId);

        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        context.Response.Headers.RetryAfter = RetryAfterSeconds;

        return await _problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status503ServiceUnavailable,
                Title = "Datenbank vorübergehend nicht erreichbar",
                Instance = context.Request.Path,
                Extensions = { ["correlationId"] = correlationId }
            }
        });
    }
}
