using Analytics.Application.Abstractions;
using Analytics.Domain.Budgets;
using Microsoft.EntityFrameworkCore;

namespace Analytics.Infrastructure.Persistence;

public sealed class BudgetRepository : IBudgetRepository
{
    private readonly AnalyticsDbContext _context;

    public BudgetRepository(AnalyticsDbContext context)
    {
        _context = context;
    }

    public Task<MonthlyBudget?> FindAsync(string ownerId, CancellationToken cancellationToken)
    {
        return _context.Budgets.SingleOrDefaultAsync(budget => budget.OwnerId == ownerId, cancellationToken);
    }

    public Task<bool> ExistsAsync(string ownerId, CancellationToken cancellationToken)
    {
        return _context.Budgets.AnyAsync(budget => budget.OwnerId == ownerId, cancellationToken);
    }

    public void Add(MonthlyBudget budget)
    {
        _context.Budgets.Add(budget);
    }

    public void Remove(MonthlyBudget budget)
    {
        _context.Budgets.Remove(budget);
    }

    public async Task<IReadOnlyList<BudgetAlert>> ListAlertsAsync(string ownerId, int year, int month, CancellationToken cancellationToken)
    {
        return await _context.BudgetAlerts
            .AsNoTracking()
            .Where(alert => alert.OwnerId == ownerId && alert.Year == year && alert.Month == month)
            .OrderBy(alert => alert.RaisedAt)
            .ThenBy(alert => alert.Level)
            .ToListAsync(cancellationToken);
    }

    public void AddAlert(BudgetAlert alert)
    {
        _context.BudgetAlerts.Add(alert);
    }
}
