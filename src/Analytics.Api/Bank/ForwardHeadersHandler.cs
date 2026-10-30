using Analytics.Api.Middleware;

namespace Analytics.Api.Bank;

// Reicht Authorization und Korrelations-Id der laufenden Anfrage an die Bank weiter. Die
// Analytics-Firma handelt nie im eigenen Namen: sie trägt weiter, was der Kunde ihr bei "Sign in
// with Bank" gegeben hat, und die Bank prüft selbst, ob er zugestimmt hat.
//
// Als Handler, weil der erzeugte Client diese Köpfe nicht kennt. Läuft kein HTTP-Aufruf, etwa im
// Konsumenten der Monatsberichte, gibt es nichts weiterzureichen.
public sealed class ForwardHeadersHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _context;

    public ForwardHeadersHandler(IHttpContextAccessor context)
    {
        _context = context;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        HttpContext? current = _context.HttpContext;
        if (current is null)
        {
            return base.SendAsync(request, cancellationToken);
        }

        string? authorization = current.Request.Headers.Authorization.FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(authorization))
        {
            request.Headers.Remove("Authorization");
            request.Headers.TryAddWithoutValidation("Authorization", authorization);
        }

        request.Headers.Remove(CorrelationId.HeaderName);
        request.Headers.TryAddWithoutValidation(CorrelationId.HeaderName, CorrelationId.Of(current));

        return base.SendAsync(request, cancellationToken);
    }
}
