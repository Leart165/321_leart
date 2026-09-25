namespace Analytics.Application.Ledger;

public sealed record DailyTotal(
    DateOnly Day,
    string Currency,
    decimal Volume,
    int Deposits,
    int Withdrawals,
    int Transfers);
