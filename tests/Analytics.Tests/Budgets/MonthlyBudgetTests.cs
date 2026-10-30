using Analytics.Domain.Budgets;
using Analytics.Domain.Exceptions;
using Analytics.Domain.Ledger;
using Xunit;

namespace Analytics.Tests.Budgets;

// Das Monatsbudget als Aggregat: welche Beträge gelten und ab wann gewarnt wird.
public sealed class MonthlyBudgetTests
{
    private static readonly DateTimeOffset Now = new DateTimeOffset(2026, 10, 30, 9, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(0, BudgetLevel.Ok)]
    [InlineData(799.99, BudgetLevel.Ok)]
    [InlineData(800, BudgetLevel.Warning)]
    [InlineData(999.99, BudgetLevel.Warning)]
    [InlineData(1000, BudgetLevel.Exceeded)]
    [InlineData(1500, BudgetLevel.Exceeded)]
    public void From_80_percent_it_warns_and_from_100_percent_it_is_exceeded(decimal spent, BudgetLevel expected)
    {
        MonthlyBudget budget = MonthlyBudget.Set("kunde-1", Currency.Of("CHF"), 1000m, Now);

        Assert.Equal(expected, budget.LevelFor(spent));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1_000_000.01)]
    [InlineData(10.005)]
    public void A_limit_outside_the_rules_is_rejected(decimal limit)
    {
        Assert.Throws<InvalidBudgetException>(() => MonthlyBudget.Set("kunde-1", Currency.Of("CHF"), limit, Now));
    }

    [Fact]
    public void A_budget_without_owner_is_rejected()
    {
        Assert.Throws<InvalidBudgetException>(() => MonthlyBudget.Set(" ", Currency.Of("CHF"), 100m, Now));
    }

    [Fact]
    public void A_change_replaces_currency_limit_and_time()
    {
        MonthlyBudget budget = MonthlyBudget.Set(" kunde-1 ", Currency.Of("CHF"), 1000m, Now);

        budget.Change(Currency.Of("EUR"), 250.50m, Now.AddHours(1));

        Assert.Equal("kunde-1", budget.OwnerId);
        Assert.Equal("EUR", budget.Currency.Code);
        Assert.Equal(250.50m, budget.Limit);
        Assert.Equal(Now.AddHours(1), budget.UpdatedAt);
        Assert.Throws<InvalidBudgetException>(() => budget.Change(Currency.Of("EUR"), 0m, Now));
    }

    [Fact]
    public void Below_80_percent_there_is_no_alert()
    {
        MonthlyBudget budget = MonthlyBudget.Set("kunde-1", Currency.Of("CHF"), 1000m, Now);

        Assert.Throws<ArgumentOutOfRangeException>(() => BudgetAlert.Raise(budget, 2026, 10, BudgetLevel.Ok, 100m, Now));

        BudgetAlert alert = BudgetAlert.Raise(budget, 2026, 10, BudgetLevel.Warning, 850m, Now);
        Assert.Equal("kunde-1", alert.OwnerId);
        Assert.Equal(1000m, alert.Limit);
        Assert.Equal(850m, alert.Spent);
    }
}
