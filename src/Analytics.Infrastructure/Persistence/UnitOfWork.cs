using Analytics.Application.Abstractions;
using Analytics.Application.Reports;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Analytics.Infrastructure.Persistence;

public sealed class UnitOfWork : IUnitOfWork
{
    private const string UniqueViolation = "23505";

    private readonly AnalyticsDbContext _context;

    public UnitOfWork(AnalyticsDbContext context)
    {
        _context = context;
    }

    // SaveChanges läuft in EF Core schon in einer Transaktion: Bericht und Outbox-Eintrag werden
    // zusammen gültig oder gar nicht.
    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
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
