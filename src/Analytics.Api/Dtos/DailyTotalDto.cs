using Analytics.Application.Ledger;

namespace Analytics.Api.Dtos;

public sealed record DailyTotalDto(
    DateOnly Day,
    string Currency,
    decimal Volume,
    int Deposits,
    int Withdrawals,
    int Transfers)
{
    public static DailyTotalDto From(DailyTotal total)
    {
        return new DailyTotalDto(
            total.Day,
            total.Currency,
            total.Volume,
            total.Deposits,
            total.Withdrawals,
            total.Transfers);
    }
}
