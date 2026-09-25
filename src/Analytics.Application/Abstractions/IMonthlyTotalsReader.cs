using Analytics.Application.Ledger;

namespace Analytics.Application.Abstractions;

public interface IMonthlyTotalsReader
{
    Task<IReadOnlyList<MonthlyTotal>> GetForOwnerAsync(string ownerId, int year, CancellationToken cancellationToken);
}
