using Analytics.Application.Abstractions;
using Analytics.Application.Ledger;
using Microsoft.EntityFrameworkCore;

namespace Analytics.Infrastructure.Persistence;

public sealed class BookingLogReader : IBookingLogReader
{
    private readonly AnalyticsDbContext _context;

    public BookingLogReader(AnalyticsDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<BookingEntry>> GetForOwnerAsync(
        string ownerId,
        DateOnly from,
        DateOnly to,
        int limit,
        CancellationToken cancellationToken)
    {
        // Tage in UTC, wie die Summen: eine Buchung am 30.9. um 23:30 UTC gehört zum 30.9.
        DateTimeOffset start = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        DateTimeOffset end = new DateTimeOffset(to.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

        return await _context.OwnerBookings
            .Where(booking => booking.OwnerId == ownerId && booking.BookedAt >= start && booking.BookedAt < end)
            .OrderByDescending(booking => booking.BookedAt)
            .ThenBy(booking => booking.TransactionId)
            .Take(limit)
            .Select(booking => new BookingEntry(
                booking.TransactionId,
                booking.Kind,
                booking.Amount,
                booking.Currency,
                booking.BookedAt))
            .ToListAsync(cancellationToken);
    }
}
