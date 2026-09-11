using Auth.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Auth.Api.Middleware;

public sealed class DomainExceptionHandler : IExceptionHandler
{
    private readonly IProblemDetailsService _problemDetails;
    private readonly ILogger<DomainExceptionHandler> _logger;

    public DomainExceptionHandler(IProblemDetailsService problemDetails, ILogger<DomainExceptionHandler> logger)
    {
        _problemDetails = problemDetails;
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext context,
        Exception exception,
        CancellationToken cancellationToken)
    {
        (int status, string title) = Translate(exception);
        if (status == 0)
        {
            return false;
        }

        string correlationId = CorrelationId.Of(context);

        _logger.LogWarning(
            exception,
            "Anfrage {Method} {Path} endet mit {Status}. Korrelation {CorrelationId}.",
            context.Request.Method, context.Request.Path, status, correlationId);

        if (context.Response.HasStarted)
        {
            return false;
        }

        context.Response.StatusCode = status;

        return await _problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = exception.Message,
                Instance = context.Request.Path,
                Extensions = { ["correlationId"] = correlationId }
            }
        });
    }

    private static (int Status, string Title) Translate(Exception exception)
    {
        return exception switch
        {
            DomainValidationException => (StatusCodes.Status400BadRequest, "Eingabe ungültig"),
            UpstreamServiceException => (StatusCodes.Status502BadGateway, "Kontendienst nicht erreichbar"),

            _ => (0, string.Empty)
        };
    }
}
