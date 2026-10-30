namespace Analytics.Infrastructure.Bank;

// Wie geduldig der Dienst mit der Bank ist. Überschreiben unter Bank:Resilience, im Compose etwa
// mit Bank__Resilience__BreakDuration=00:00:05 für eine kürzere Pause in der Demo.
public sealed class BankResilienceOptions
{
    public const string SectionName = "Bank:Resilience";

    // So lange wartet eine Anfrage höchstens auf die Bank, alle Versuche zusammen. Danach kommt
    // der gespeicherte Stand oder 503: lieber eine schnelle Antwort als eine, die hängt.
    public TimeSpan TotalTimeout { get; set; } = TimeSpan.FromSeconds(5);

    // Obergrenze für einen einzelnen Versuch. Ein Jahr Buchungen eines Kunden dauert Millisekunden.
    public TimeSpan AttemptTimeout { get; set; } = TimeSpan.FromSeconds(2);

    // Wiederholungen nach dem ersten Versuch, nur bei vorübergehenden Fehlern.
    public int MaxRetryAttempts { get; set; } = 2;

    // Pause vor der ersten Wiederholung, danach jeweils doppelt so lang, mit Streuung.
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromMilliseconds(100);

    // Anteil gescheiterter Versuche im Zeitfenster, ab dem der Kreis öffnet. Zusammen mit
    // MinimumThroughput heisst das: fällt die Bank ganz aus, öffnet der Kreis nach fünf Fehlversuchen.
    public double FailureRatio { get; set; } = 0.5;

    public int MinimumThroughput { get; set; } = 5;

    public TimeSpan SamplingDuration { get; set; } = TimeSpan.FromSeconds(30);

    // So lange bleibt der Kreis offen. Danach geht ein Probeaufruf durch: gelingt er, ist der Kreis
    // wieder zu, sonst wieder offen. Auch der Wert für Retry-After, solange er offen ist.
    public TimeSpan BreakDuration { get; set; } = TimeSpan.FromSeconds(15);

    // Retry-After nach einem einzelnen Ausfall, wenn die Bank selbst keine Wartezeit nennt.
    public TimeSpan RetryAfter { get; set; } = TimeSpan.FromSeconds(5);
}
