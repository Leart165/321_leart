using System.Diagnostics.Metrics;

namespace Analytics.Infrastructure.Messaging;

public static class MessagingMetrics
{
    public const string MeterName = "Analytics.Messaging";

    private static readonly Meter Meter = new Meter(MeterName);

    // Derselbe Name wie bei den Konsumenten der Bank: das gemeinsame Dashboard gruppiert nur
    // nach queue und outcome und zeigt analytics.ledger so neben statements.ledger.
    private static readonly Counter<long> Consumed = Meter.CreateCounter<long>(
        "bank.messages.consumed",
        unit: "{message}",
        description: "Empfangene Nachrichten, nach Queue und Ergebnis");

    public static void RecordConsumed(string queue, string outcome)
    {
        Consumed.Add(
            1,
            new KeyValuePair<string, object?>("queue", queue),
            new KeyValuePair<string, object?>("outcome", outcome));
    }
}
