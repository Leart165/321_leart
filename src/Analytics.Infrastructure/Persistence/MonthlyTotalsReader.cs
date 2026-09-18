using Analytics.Application.Abstractions;
using Analytics.Application.Ledger;
using Microsoft.EntityFrameworkCore;

namespace Analytics.Infrastructure.Persistence;

public sealed class MonthlyTotalsReader : IMonthlyTotalsReader
{
    private readonly AnalyticsDbContext _context;

    public MonthlyTotalsReader(AnalyticsDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<MonthlyTotal>> GetForOwnerAsync(
        string ownerId,
        int year,
        CancellationToken cancellationToken)
    {
        return await _context.OwnerMonthly
            .Where(monthly => monthly.OwnerId == ownerId && monthly.Year == year)
            .OrderBy(monthly => monthly.Month)
            .ThenBy(monthly => monthly.Currency)
            .Select(monthly => new MonthlyTotal(
                monthly.Year,
                monthly.Month,
                monthly.Currency,
                monthly.Income,
                monthly.Expenses,
                monthly.Transactions))
            .ToListAsync(cancellationToken);
    }
}
