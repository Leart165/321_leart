using Analytics.Application.Abstractions;
using Analytics.Application.Ledger;
using Analytics.Domain.Ledger;
using Microsoft.EntityFrameworkCore;

namespace Analytics.Infrastructure.Persistence;

// Tagessummen über die Buchungen, die Kunden mit ihrer Zustimmung bei uns gespeichert haben.
// Bankweite Zahlen gibt die Bank keinem fremden Client, auch nicht einem Mitarbeiter mit
// bank-admin; was hier steht, sind die eigenen Daten der Analytics-Firma.
public sealed class DailyTotalsReader : IDailyTotalsReader
{
    private readonly AnalyticsDbContext _context;

    public DailyTotalsReader(AnalyticsDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<DailyTotal>> GetForRangeAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        DateTimeOffset start = UtcDays.StartOf(from);
        DateTimeOffset end = UtcDays.StartOf(to.AddDays(1));

        List<OwnerBooking> bookings = await _context.OwnerBookings
            .AsNoTracking()
            .Where(booking => booking.BookedAt >= start && booking.BookedAt < end)
            .ToListAsync(cancellationToken);

        return LedgerTotals.Daily(bookings.Select(booking => BookedTransaction.Of(
            booking.TransactionId,
            booking.OwnerId,
            Enum.Parse<TransactionKind>(booking.Kind),
            booking.Amount,
            Currency.Of(booking.Currency),
            booking.BookedAt)));
    }
}
