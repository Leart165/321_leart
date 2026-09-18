using Analytics.Application.Ledger;

namespace Analytics.Application.Abstractions;

public interface IDailyTotalsReader
{
    Task<IReadOnlyList<DailyTotal>> GetForRangeAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken);
}
