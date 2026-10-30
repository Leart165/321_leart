namespace Analytics.Tests.Budgets;

// Eine Uhr, die steht, bis ein Test sie stellt.
public sealed class FixedClock : TimeProvider
{
    public FixedClock(DateTimeOffset now)
    {
        Now = now;
    }

    public DateTimeOffset Now { get; set; }

    public override DateTimeOffset GetUtcNow()
    {
        return Now;
    }
}
