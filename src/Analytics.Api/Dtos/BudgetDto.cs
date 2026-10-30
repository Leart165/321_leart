using Analytics.Application.Budgets;
using Analytics.Domain.Budgets;

namespace Analytics.Api.Dtos;

// Das Budget und wie weit es im laufenden Monat ausgeschöpft ist. percent ist gerundet;
// level ok, warning ab 80, exceeded ab 100 Prozent.
public sealed record BudgetDto(
    string Currency,
    decimal MonthlyLimit,
    int Year,
    int Month,
    decimal Spent,
    int Percent,
    string Level,
    IReadOnlyList<BudgetAlertDto> Alerts)
{
    public static BudgetDto From(BudgetStatus status)
    {
        return new BudgetDto(
            status.Budget.Currency.Code,
            status.Budget.Limit,
            status.Year,
            status.Month,
            status.Spent,
            (int)Math.Floor(status.Ratio * 100m),
            LevelName(status.Level),
            status.Alerts.Select(BudgetAlertDto.From).ToList());
    }

    public static string LevelName(BudgetLevel level)
    {
        return level.ToString().ToLowerInvariant();
    }
}

public sealed record BudgetAlertDto(string Level, decimal Spent, decimal MonthlyLimit, string Currency, DateTimeOffset RaisedAt)
{
    public static BudgetAlertDto From(BudgetAlert alert)
    {
        return new BudgetAlertDto(BudgetDto.LevelName(alert.Level), alert.Spent, alert.Limit, alert.Currency.Code, alert.RaisedAt);
    }
}
