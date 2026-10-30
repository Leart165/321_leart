using Analytics.Application.Ledger;

namespace Analytics.Application.Bank;

// Die Buchungen des angemeldeten Kunden bei der Bank, gelesen mit seinem Token. Wessen Buchungen
// es sind, entscheidet die Bank aus dem sub im Token; der Dienst nennt den Kunden nicht.
//
// Lehnt die Bank das Token ab, wirft es BankAccessDeniedException. Ist sie nicht zu erreichen,
// BankUnavailableException.
public interface IBankBookings
{
    // Zeitraum halboffen und in UTC: from gehört dazu, to nicht. Sortiert nach bookedAt.
    Task<IReadOnlyList<BookingEntry>> GetAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken);
}
