using Analytics.Application.Abstractions;
using Analytics.Application.Ledger;
using Microsoft.EntityFrameworkCore;

namespace Analytics.Infrastructure.Persistence;

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
        return await _context.SystemDaily
            .Where(daily => daily.Day >= from && daily.Day <= to)
            .OrderBy(daily => daily.Day)
            .ThenBy(daily => daily.Currency)
            .Select(daily => new DailyTotal(
                daily.Day,
                daily.Currency,
                daily.Volume,
                daily.Deposits,
                daily.Withdrawals,
                daily.Transfers))
            .ToListAsync(cancellationToken);
    }
}
