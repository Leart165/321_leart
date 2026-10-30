using Analytics.Infrastructure.Bank.Generated;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Analytics.Tests.Bank;

// Ein Aufruf, den der Dienst an die Bank geschickt hat, mit from und to so, wie sie im Pfad standen.
public sealed record BankCall(string Path, string? From, string? To, string? Authorization, string? CorrelationId);

// Spielt den Auszugsdienst der Bank nach contracts/statements/openapi.v2.yaml: liefert die
// Buchungen des Kunden aus dem sub im Token, im halboffenen Zeitraum, sortiert nach bookedAt, und
// merkt sich jeden Aufruf. Ersetzt wird nur der letzte Handler in der Kette; Token-Weitergabe,
// Wiederholung und Kreis laufen davor wie im Betrieb.
public sealed class FakeStatementsApi
{
    private static readonly Regex Zoned = new Regex(@"(Z|[+-]\d{2}:\d{2})$");

    private readonly object _lock = new object();
    private readonly List<BankCall> _calls = new List<BankCall>();
    private readonly List<(string Owner, Guid Id, string Kind, decimal Amount, string Currency, DateTimeOffset BookedAt)> _bookings =
        new List<(string, Guid, string, decimal, string, DateTimeOffset)>();

    private Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>? _instead;

    public IReadOnlyList<BankCall> Calls
    {
        get
        {
            lock (_lock)
            {
                return _calls.ToList();
            }
        }
    }

    public Guid Book(string owner, string kind, decimal amount, DateTimeOffset bookedAt, string currency = "CHF")
    {
        Guid id = Guid.NewGuid();
        lock (_lock)
        {
            _bookings.Add((owner, id, kind, amount, currency, bookedAt));
        }

        return id;
    }

    public void AnswerNormally()
    {
        _instead = null;
    }

    public void AnswerStatus(HttpStatusCode status)
    {
        _instead = Synchronously(_ => new HttpResponseMessage(status));
    }

    // Wie Traefik, wenn es für den Pfad keine Route gibt.
    public void AnswerNoRoute()
    {
        _instead = Synchronously(_ => new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent("404 page not found\n", Encoding.UTF8, "text/plain")
        });
    }

    // Eine Antwort, die zum Schema passt, aber eine Buchung ohne positiven Betrag enthält.
    public void AnswerBrokenBooking()
    {
        _instead = Synchronously(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new
            {
                from = DateTimeOffset.UnixEpoch,
                to = DateTimeOffset.UnixEpoch.AddDays(1),
                bookings = new[]
                {
                    new { transactionId = Guid.NewGuid(), kind = "Deposit", amount = -5m, currency = "CHF", bookedAt = DateTimeOffset.UnixEpoch }
                }
            })
        });
    }

    // Wie ein gestoppter Container.
    public void BeUnreachable()
    {
        _instead = Synchronously(_ => throw new HttpRequestException("Connection refused (traefik:8080)"));
    }

    // Nimmt die Verbindung an und antwortet nie. Erst ein Zeitlimit beendet den Aufruf.
    public void Hang()
    {
        _instead = async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("Das Warten endet nur mit einem Abbruch.");
        };
    }

    // Scheitert beim ersten Aufruf wie bei einem kurzen Netzaussetzer und antwortet danach normal.
    public void FailOnceThenAnswer()
    {
        int attempts = 0;
        _instead = (request, cancellationToken) =>
        {
            if (Interlocked.Increment(ref attempts) == 1)
            {
                throw new HttpRequestException("Connection reset by peer");
            }

            return AnswerAsync(request, cancellationToken);
        };
    }

    public void Replace(IServiceCollection services)
    {
        services.AddHttpClient<StatementsApiClient>().ConfigurePrimaryHttpMessageHandler(() => new Handler(this));
    }

    private Task<HttpResponseMessage> HandleAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Dictionary<string, string> query = QueryOf(request);
        lock (_lock)
        {
            _calls.Add(new BankCall(
                request.RequestUri?.AbsolutePath ?? string.Empty,
                query.GetValueOrDefault("from"),
                query.GetValueOrDefault("to"),
                request.Headers.Authorization?.ToString(),
                request.Headers.TryGetValues("X-Correlation-Id", out IEnumerable<string>? values) ? values.FirstOrDefault() : null));
        }

        return (_instead ?? AnswerAsync)(request, cancellationToken);
    }

    // Wie die Bank: ohne Token 401, from und to müssen Zeitpunkte mit Zeitzone sein, from vor to.
    private Task<HttpResponseMessage> AnswerAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        AuthenticationHeaderValue? authorization = request.Headers.Authorization;
        if (authorization?.Parameter is null)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized));
        }

        Dictionary<string, string> query = QueryOf(request);
        if (!query.TryGetValue("from", out string? rawFrom) || !query.TryGetValue("to", out string? rawTo)
            || !Zoned.IsMatch(rawFrom) || !Zoned.IsMatch(rawTo))
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest));
        }

        DateTimeOffset from = DateTimeOffset.Parse(rawFrom);
        DateTimeOffset to = DateTimeOffset.Parse(rawTo);
        if (from >= to || (to - from).TotalDays > 366)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest));
        }

        string owner = SubjectOf(authorization.Parameter);
        List<object> bookings;
        lock (_lock)
        {
            bookings = _bookings
                .Where(booking => booking.Owner == owner && booking.BookedAt >= from && booking.BookedAt < to)
                .OrderBy(booking => booking.BookedAt)
                .Select(booking => (object)new
                {
                    transactionId = booking.Id,
                    kind = booking.Kind,
                    amount = booking.Amount,
                    currency = booking.Currency,
                    bookedAt = booking.BookedAt
                })
                .ToList();
        }

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new { from = from.ToUniversalTime(), to = to.ToUniversalTime(), bookings })
        });
    }

    private static Dictionary<string, string> QueryOf(HttpRequestMessage request)
    {
        return (request.RequestUri?.Query ?? string.Empty)
            .TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(pair => pair.Split('=', 2))
            .ToDictionary(pair => Uri.UnescapeDataString(pair[0]), pair => pair.Length > 1 ? Uri.UnescapeDataString(pair[1]) : string.Empty);
    }

    private static string SubjectOf(string token)
    {
        string payload = token.Split('.')[1].Replace('-', '+').Replace('_', '/');
        payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
        using JsonDocument claims = JsonDocument.Parse(Convert.FromBase64String(payload));
        return claims.RootElement.GetProperty("sub").GetString() ?? string.Empty;
    }

    private static Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> Synchronously(
        Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        return (request, _) => Task.FromResult(respond(request));
    }

    private sealed class Handler : HttpMessageHandler
    {
        private readonly FakeStatementsApi _api;

        public Handler(FakeStatementsApi api)
        {
            _api = api;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return _api.HandleAsync(request, cancellationToken);
        }
    }
}
