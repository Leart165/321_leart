namespace Analytics.Infrastructure.Messaging;

public static class ConsumeOutcome
{
    public const string Processed = "processed";

    public const string Duplicate = "duplicate";

    public const string Retried = "retried";

    public const string DeadLettered = "dead_lettered";

    public static bool IsAcknowledged(string outcome)
    {
        return outcome is Processed or Duplicate;
    }
}
