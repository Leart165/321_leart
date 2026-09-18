namespace Analytics.Application.Ledger;

public sealed record MonthlyTotal(
    int Year,
    int Month,
    string Currency,
    decimal Income,
    decimal Expenses,
    int Transactions);
