namespace Analytics.Application.Ledger;

// Tage gelten in UTC, wie bei der Bank: eine Buchung am 30.9. um 23:30 UTC gehört zum 30.9.
public static class UtcDays
{
    public static DateTimeOffset StartOf(DateOnly day)
    {
        return new DateTimeOffset(day.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
    }
}
