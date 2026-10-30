using Analytics.Domain.Budgets;

namespace Analytics.Application.Abstractions;

public interface IBudgetRepository
{
    Task<MonthlyBudget?> FindAsync(string ownerId, CancellationToken cancellationToken);

    Task<bool> ExistsAsync(string ownerId, CancellationToken cancellationToken);

    void Add(MonthlyBudget budget);

    void Remove(MonthlyBudget budget);

    // Die Warnungen eines Monats, älteste zuerst.
    Task<IReadOnlyList<BudgetAlert>> ListAlertsAsync(string ownerId, int year, int month, CancellationToken cancellationToken);

    void AddAlert(BudgetAlert alert);
}
