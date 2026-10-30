using Analytics.Application.Abstractions;
using Analytics.Application.Bank;

namespace Analytics.Application.Ledger;

// Die Buchungen eines Kunden, gelesen bei der Bank mit seinem Token und nur mit seiner Zustimmung.
//
// Die Bank ist die Quelle. Was sie liefert, merkt sich der Dienst, damit er bei einem Ausfall der
// Bank den zuletzt bekannten Stand zeigen kann statt gar nichts. Das gilt nur, solange der Kunde
// zustimmt: fehlt die Zustimmung im Token oder lehnt die Bank ab, wird alles Gespeicherte dieses
// Kunden gelöscht und nichts gezeigt.
public sealed class CustomerLedger
{
    private readonly IBankBookings _bank;
    private readonly IBookingStore _store;
    private readonly IBookingLogReader _log;

    public CustomerLedger(IBankBookings bank, IBookingStore store, IBookingLogReader log)
    {
        _bank = bank;
        _store = store;
        _log = log;
    }

    // Tage in UTC, beide Grenzen eingeschlossen. Ist die Bank weg und von diesem Kunden etwas
    // gespeichert, kommt der gespeicherte Stand; sonst wirft es BankUnavailableException.
    public async Task<LedgerSnapshot> GetBookingsAsync(
        string ownerId,
        bool consented,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<BookingEntry> live;
        try
        {
            live = await FetchAsync(ownerId, consented, UtcDays.StartOf(from), UtcDays.StartOf(to.AddDays(1)), cancellationToken);
        }
        catch (BankUnavailableException)
        {
            if (!await _store.KnowsAsync(ownerId, cancellationToken))
            {
                throw;
            }

            IReadOnlyList<BookingEntry> stored = await _log.GetForOwnerAsync(ownerId, from, to, int.MaxValue, cancellationToken);
            return new LedgerSnapshot(stored, BookingSource.Stored, 0);
        }

        int newBookings = await _store.TryRememberAsync(ownerId, live.ToList(), cancellationToken);
        return new LedgerSnapshot(live, BookingSource.Live, newBookings);
    }

    public async Task<MonthlyOverview> GetMonthlyAsync(
        string ownerId,
        bool consented,
        int year,
        CancellationToken cancellationToken)
    {
        LedgerSnapshot snapshot = await GetBookingsAsync(
            ownerId, consented, new DateOnly(year, 1, 1), new DateOnly(year, 12, 31), cancellationToken);

        IReadOnlyList<MonthlyTotal> months = LedgerTotals.Monthly(
            snapshot.Bookings.Select(entry => entry.ToTransaction(ownerId)));
        return new MonthlyOverview(months, snapshot.Source, snapshot.NewBookings);
    }

    // Nur frisch von der Bank, ohne gespeicherten Stand. Zeitraum halboffen in UTC.
    public async Task<IReadOnlyList<BookingEntry>> FetchAsync(
        string ownerId,
        bool consented,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken)
    {
        if (!consented)
        {
            await _store.TryForgetAsync(ownerId, cancellationToken);
            throw BankAccessDeniedException.WithoutConsent();
        }

        try
        {
            return await _bank.GetAsync(from, to, cancellationToken);
        }
        catch (BankAccessDeniedException)
        {
            await _store.TryForgetAsync(ownerId, cancellationToken);
            throw;
        }
    }
}
