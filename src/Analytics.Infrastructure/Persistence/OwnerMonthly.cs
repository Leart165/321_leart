namespace Analytics.Infrastructure.Persistence;

public sealed class OwnerMonthly
{
    public string OwnerId { get; set; } = string.Empty;

    public int Year { get; set; }

    public int Month { get; set; }

    public string Currency { get; set; } = string.Empty;

    public decimal Income { get; set; }

    public decimal Expenses { get; set; }

    public int Transactions { get; set; }
}
