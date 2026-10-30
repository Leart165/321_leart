namespace Analytics.Application.Bank;

// Die Bank ist gerade nicht zu erreichen: keine Verbindung, Zeitlimit, 5xx oder offener Kreis.
// Ein vorübergehender Zustand; RetryAfter sagt, wann sich ein neuer Versuch lohnt.
public sealed class BankUnavailableException : Exception
{
    public BankUnavailableException(string message, TimeSpan retryAfter, Exception? innerException = null)
        : base(message, innerException)
    {
        RetryAfter = retryAfter;
    }

    public TimeSpan RetryAfter { get; }
}
