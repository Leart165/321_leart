using Analytics.Api.Authentication;
using Analytics.Application.Bank;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;

namespace Analytics.Api.Middleware;

// Übersetzt, was die Bank sagt, in eine Antwort an den Kunden.
//
// Absage der Bank oder keine Zustimmung im Token: 403 wie bei einem fehlenden Scope, nach
// RFC 6750 mit insufficient_scope und scope="statements:read", ohne Körper. Die Seite weiss so,
// dass nur eine neue Zustimmung bei der Bank hilft.
//
// Bank nicht erreichbar und kein gespeicherter Stand: 503 mit Retry-After, nie 500. Der Fehler
// liegt nicht beim Aufrufer und nicht bei diesem Dienst.
public sealed class BankFailureHandler : IExceptionHandler
{
    private readonly IProblemDetailsService _problemDetails;
    private readonly ILogger<BankFailureHandler> _logger;

    public BankFailureHandler(IProblemDetailsService problemDetails, ILogger<BankFailureHandler> logger)
    {
        _problemDetails = problemDetails;
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        if (context.Response.HasStarted)
        {
            return false;
        }

        switch (exception)
        {
            case BankAccessDeniedException:
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.Headers.WWWAuthenticate =
                    $"Bearer error=\"insufficient_scope\", scope=\"{Scopes.StatementsRead}\"";
                return true;

            case BankUnavailableException unavailable:
                string correlationId = CorrelationId.Of(context);
                _logger.LogWarning(
                    "Anfrage {Method} {Path} endet mit 503, die Bank ist nicht erreichbar und nichts gespeichert. Korrelation {CorrelationId}.",
                    context.Request.Method, context.Request.Path, correlationId);

                context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                context.Response.Headers.RetryAfter =
                    Math.Max(1, (int)Math.Ceiling(unavailable.RetryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);

                return await _problemDetails.TryWriteAsync(new ProblemDetailsContext
                {
                    HttpContext = context,
                    Exception = exception,
                    ProblemDetails = new ProblemDetails
                    {
                        Status = StatusCodes.Status503ServiceUnavailable,
                        Title = "Bank vorübergehend nicht erreichbar",
                        Detail = "Die Buchungen kommen von der Bank, und sie antwortet gerade nicht.",
                        Instance = context.Request.Path,
                        Extensions = { ["correlationId"] = correlationId }
                    }
                });

            default:
                return false;
        }
    }
}
