using Analytics.Application.Abstractions;
using Analytics.Application.Ledger;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Analytics.Infrastructure.Persistence;

// Die Buchungen, die die Bank zu einem Kunden geliefert hat. Geschrieben wird mit einem einzigen
// INSERT ... ON CONFLICT DO NOTHING: dieselbe Buchung zweimal, auch von zwei Instanzen zugleich,
// steht einmal da.
public sealed class BookingStore : IBookingStore
{
    private readonly AnalyticsDbContext _context;
    private readonly ILogger<BookingStore> _logger;
    private readonly List<OwnerBooking> _staged = new List<OwnerBooking>();

    public BookingStore(AnalyticsDbContext context, ILogger<BookingStore> logger)
    {
        _context = context;
        _logger = logger;
    }

    internal bool HasStaged
    {
        get { return _staged.Count > 0; }
    }

    public void Stage(string ownerId, IEnumerable<BookingEntry> bookings)
    {
        _staged.AddRange(bookings.Select(booking => new OwnerBooking
        {
            TransactionId = booking.TransactionId,
            OwnerId = ownerId,
            Kind = booking.Kind,
            Amount = booking.Amount,
            Currency = booking.Currency,
            BookedAt = booking.BookedAt.ToUniversalTime()
        }));
    }

    public async Task<bool> TryRememberAsync(string ownerId, IReadOnlyCollection<BookingEntry> bookings, CancellationToken cancellationToken)
    {
        Stage(ownerId, bookings);
        try
        {
            await FlushAsync(cancellationToken);
            return true;
        }
        catch (Exception exception) when (DatabaseFailures.IsTransient(exception))
        {
            _staged.Clear();
            _logger.LogWarning(
                "Buchungen von {Owner} nicht gespeichert, die Datenbank ist nicht erreichbar ({Error}).",
                ownerId, exception.GetBaseException().Message);
            return false;
        }
    }

    public async Task<bool> TryForgetAsync(string ownerId, CancellationToken cancellationToken)
    {
        try
        {
            int removed = await _context.Database.ExecuteSqlAsync(
                $"DELETE FROM owner_bookings WHERE owner_id = {ownerId}", cancellationToken);
            if (removed > 0)
            {
                _logger.LogInformation(
                    "Keine Zustimmung mehr: {Count} gespeicherte Buchungen von {Owner} gelöscht.", removed, ownerId);
            }

            return true;
        }
        catch (Exception exception) when (DatabaseFailures.IsTransient(exception))
        {
            _logger.LogWarning(
                "Gespeicherte Buchungen von {Owner} nicht gelöscht, die Datenbank ist nicht erreichbar ({Error}).",
                ownerId, exception.GetBaseException().Message);
            return false;
        }
    }

    public Task<bool> KnowsAsync(string ownerId, CancellationToken cancellationToken)
    {
        return _context.OwnerBookings.AnyAsync(booking => booking.OwnerId == ownerId, cancellationToken);
    }

    // Schreibt das Vorgemerkte. Eine einzige Anweisung, also für sich schon ganz oder gar nicht;
    // in UnitOfWork läuft sie zusammen mit SaveChanges in einer Transaktion.
    internal async Task FlushAsync(CancellationToken cancellationToken)
    {
        if (_staged.Count == 0)
        {
            return;
        }

        Guid[] ids = _staged.Select(booking => booking.TransactionId).ToArray();
        string[] owners = _staged.Select(booking => booking.OwnerId).ToArray();
        string[] kinds = _staged.Select(booking => booking.Kind).ToArray();
        decimal[] amounts = _staged.Select(booking => booking.Amount).ToArray();
        string[] currencies = _staged.Select(booking => booking.Currency).ToArray();
        DateTimeOffset[] bookedAt = _staged.Select(booking => booking.BookedAt).ToArray();
        _staged.Clear();

        await _context.Database.ExecuteSqlAsync(
            $@"INSERT INTO owner_bookings (transaction_id, owner_id, kind, amount, currency, booked_at)
               SELECT * FROM unnest({ids}, {owners}, {kinds}, {amounts}, {currencies}, {bookedAt})
               ON CONFLICT (transaction_id) DO NOTHING",
            cancellationToken);
    }
}
