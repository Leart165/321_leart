namespace Analytics.Infrastructure.Persistence;

public sealed class SystemDaily
{
    public DateOnly Day { get; set; }

    public string Currency { get; set; } = string.Empty;

    public decimal Volume { get; set; }

    public int Deposits { get; set; }

    public int Withdrawals { get; set; }

    public int Transfers { get; set; }
}
