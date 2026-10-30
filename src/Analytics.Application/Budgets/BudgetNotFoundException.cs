namespace Analytics.Application.Budgets;

public sealed class BudgetNotFoundException : Exception
{
    public BudgetNotFoundException()
        : base("Für diesen Kunden ist kein Budget festgelegt.")
    {
    }
}
