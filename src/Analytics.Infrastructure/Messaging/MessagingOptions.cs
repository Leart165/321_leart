namespace Analytics.Infrastructure.Messaging;

public sealed class MessagingOptions
{
    public const string SectionName = "RabbitMq";

    public string Host { get; set; } = "localhost";

    public int Port { get; set; } = 5672;

    public string User { get; set; } = "guest";

    public string Password { get; set; } = "guest";

    public string VirtualHost { get; set; } = "/";

    public string ClientName { get; set; } = "analytics-api";

    public ushort Prefetch { get; set; } = 10;

    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(2);

    // Was mit der alten Queue analytics.ledger geschieht, siehe contracts/analytics/asyncapi.v2.yaml.
    public LegacyQueueMode LegacyQueue { get; set; } = LegacyQueueMode.Drain;
}

public enum LegacyQueueMode
{
    // Expand: die alte Queue weiter lesen, falls es sie gibt, neben der Partner-Queue.
    Drain,

    // Contract: Bindung lösen, den Rest lesen, dann Queue und Dead-Letter-Queue löschen.
    Retire
}
