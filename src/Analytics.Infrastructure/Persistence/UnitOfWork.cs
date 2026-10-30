using Analytics.Application.Abstractions;
using Analytics.Application.Reports;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace Analytics.Infrastructure.Persistence;

public sealed class UnitOfWork : IUnitOfWork
{
    private const string UniqueViolation = "23505";

    private readonly AnalyticsDbContext _context;
    private readonly BookingStore _bookings;

    public UnitOfWork(AnalyticsDbContext context, BookingStore bookings)
    {
        _context = context;
        _bookings = bookings;
    }

    // Bericht, Outbox-Eintrag und die vorgemerkten Buchungen der Bank werden zusammen gültig oder
    // gar nicht. Ohne vorgemerkte Buchungen reicht die Transaktion, die SaveChanges selbst öffnet.
    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (!_bookings.HasStaged)
            {
                await _context.SaveChangesAsync(cancellationToken);
                return;
            }

            await using IDbContextTransaction transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            await _bookings.FlushAsync(cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new ConcurrentChangeException("Der Datensatz wurde inzwischen von einem anderen Vorgang geändert.", exception);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: UniqueViolation })
        {
            throw new ConcurrentChangeException("Ein anderer Vorgang hat denselben Datensatz zuerst angelegt.", exception);
        }
    }
}
