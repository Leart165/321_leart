using Analytics.Application.Ledger;

namespace Analytics.Application.Abstractions;

public interface IBookingLogReader
{
    // Buchungen eines Inhabers mit Buchungsdatum im Zeitraum, beide Grenzen eingeschlossen,
    // neueste zuerst, höchstens limit Stück.
    Task<IReadOnlyList<BookingEntry>> GetForOwnerAsync(
        string ownerId,
        DateOnly from,
        DateOnly to,
        int limit,
        CancellationToken cancellationToken);
}
