using Analytics.Application.Bank;
using Analytics.Application.Ledger;
using Analytics.Domain.Exceptions;
using Analytics.Domain.Ledger;
using Analytics.Infrastructure.Bank.Generated;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly.CircuitBreaker;

namespace Analytics.Infrastructure.Bank;

// Fragt den Auszugsdienst der Bank nach den Buchungen des angemeldeten Kunden:
// GET /v2/statements/bookings aus contracts/statements/openapi.v2.yaml. Der Aufruf steckt im
// erzeugten StatementsApiClient; das Token und die Korrelations-Id der laufenden Anfrage setzt ein
// Handler der API, Wiederholung und Kreis BankResilience.
public sealed class StatementsBookings : IBankBookings
{
    private readonly StatementsApiClient _statements;
    private readonly BankResilienceOptions _options;
    private readonly ILogger<StatementsBookings> _logger;

    public StatementsBookings(StatementsApiClient statements, IOptions<BankResilienceOptions> options, ILogger<StatementsBookings> logger)
    {
        _statements = statements;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<BookingEntry>> GetAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken)
    {
        try
        {
            Bookings answer = await _statements.ListBookingsAsync(
                from.ToUniversalTime(), to.ToUniversalTime(), null, cancellationToken);
            // NSwag nennt die Liste Bookings1, weil die Klasse selbst schon Bookings heisst.
            return answer.Bookings1.Select(ToEntry).ToList();
        }
        catch (ApiException exception) when (exception.StatusCode is 401 or 403)
        {
            _logger.LogInformation(
                "Die Bank antwortet mit {Status}: der Kunde hat das Lesen seiner Buchungen nicht erlaubt.",
                exception.StatusCode);
            throw new BankAccessDeniedException($"Die Bank lehnt das Lesen der Buchungen ab ({exception.StatusCode}).");
        }
        catch (ApiException exception) when (exception.StatusCode == 400)
        {
            // Den Zeitraum bestimmt dieser Dienst. Lehnt die Bank ihn ab, liegt der Fehler hier.
            throw new InvalidOperationException(
                $"Die Bank lehnt den Zeitraum {from:o} bis {to:o} ab: {exception.Response}", exception);
        }
        catch (ApiException exception)
        {
            // Etwa 404 ohne Route bei Traefik, 5xx nach allen Versuchen, oder eine Antwort, die
            // nicht zum Kontrakt passt.
            _logger.LogWarning(
                "Die Bank antwortet mit {Status} statt mit Buchungen ({Error}).",
                exception.StatusCode, exception.Message);
            throw new BankUnavailableException(
                $"Die Bank antwortet mit {exception.StatusCode}.", RetryAfterOf(exception), exception);
        }
        catch (InvalidLedgerEventException exception)
        {
            _logger.LogError("Eine Buchung der Bank passt nicht zum Kontrakt: {Error}", exception.Message);
            throw new BankUnavailableException("Die Antwort der Bank passt nicht zum Kontrakt.", _options.RetryAfter, exception);
        }
        catch (BrokenCircuitException exception)
        {
            _logger.LogInformation("Kreis zur Bank offen, sie wird nicht gefragt.");
            throw new BankUnavailableException("Der Kreis zur Bank ist offen.", _options.BreakDuration, exception);
        }
        catch (Exception exception) when (BankResilience.IsUnavailable(exception) && !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Die Bank ist nicht erreichbar ({Error}).", exception.Message);
            throw new BankUnavailableException("Die Bank ist nicht erreichbar.", _options.RetryAfter, exception);
        }
    }

    // Nur was eine Auswertung braucht. Betrag und Währung prüft das Domain, damit eine kaputte
    // Buchung nicht still in die Summen fällt.
    private static BookingEntry ToEntry(Booking booking)
    {
        if (booking.Amount <= 0)
        {
            throw new InvalidLedgerEventException($"Die Buchung {booking.TransactionId} hat keinen positiven Betrag.");
        }

        return new BookingEntry(
            booking.TransactionId,
            booking.Kind.ToString(),
            booking.Amount,
            Currency.Of(booking.Currency).Code,
            booking.BookedAt);
    }

    private TimeSpan RetryAfterOf(ApiException exception)
    {
        if (exception.Headers.TryGetValue("Retry-After", out IEnumerable<string>? values)
            && int.TryParse(values.FirstOrDefault(), out int seconds)
            && seconds > 0)
        {
            return TimeSpan.FromSeconds(seconds);
        }

        return _options.RetryAfter;
    }
}
