using Analytics.Domain.Budgets;

namespace Analytics.Application.Budgets;

// Das Budget eines Kunden für den laufenden Monat: was er ausgegeben hat, wie weit das Budget
// ausgeschöpft ist und welche Warnungen es in diesem Monat schon gab.
public sealed record BudgetStatus(
    MonthlyBudget Budget,
    int Year,
    int Month,
    decimal Spent,
    decimal Ratio,
    BudgetLevel Level,
    IReadOnlyList<BudgetAlert> Alerts);
