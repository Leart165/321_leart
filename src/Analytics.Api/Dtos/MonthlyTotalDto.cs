using Analytics.Application.Ledger;

namespace Analytics.Api.Dtos;

public sealed record MonthlyTotalDto(
    int Year,
    int Month,
    string Currency,
    decimal Income,
    decimal Expenses,
    decimal Net,
    int Transactions)
{
    public static MonthlyTotalDto From(MonthlyTotal total)
    {
        return new MonthlyTotalDto(
            total.Year,
            total.Month,
            total.Currency,
            total.Income,
            total.Expenses,
            total.Income - total.Expenses,
            total.Transactions);
    }
}
