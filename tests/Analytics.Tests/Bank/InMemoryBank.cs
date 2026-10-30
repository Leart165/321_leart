using Analytics.Application.Bank;
using Analytics.Application.Ledger;

namespace Analytics.Tests.Bank;

// Die Bank auf der Ebene der Application, für Tests ohne HTTP: liefert, was ein Test hineinlegt,
// im halboffenen Zeitraum, oder wirft, was Failure vorgibt.
public sealed class InMemoryBank : IBankBookings
{
    public List<BookingEntry> Bookings { get; } = new List<BookingEntry>();

    public List<(DateTimeOffset From, DateTimeOffset To)> Calls { get; } = new List<(DateTimeOffset, DateTimeOffset)>();

    public Exception? Failure { get; set; }

    public Task<IReadOnlyList<BookingEntry>> GetAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken)
    {
        Calls.Add((from, to));
        if (Failure is not null)
        {
            throw Failure;
        }

        IReadOnlyList<BookingEntry> answer = Bookings
            .Where(booking => booking.BookedAt >= from && booking.BookedAt < to)
            .OrderBy(booking => booking.BookedAt)
            .ToList();
        return Task.FromResult(answer);
    }

    public BookingEntry Deposit(decimal amount, DateTimeOffset bookedAt)
    {
        BookingEntry booking = new BookingEntry(Guid.NewGuid(), "Deposit", amount, "CHF", bookedAt);
        Bookings.Add(booking);
        return booking;
    }
}
