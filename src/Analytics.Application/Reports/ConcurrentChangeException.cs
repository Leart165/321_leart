namespace Analytics.Application.Reports;

public sealed class ConcurrentChangeException : Exception
{
    public ConcurrentChangeException(string message, Exception inner)
        : base(message, inner)
    {
    }
}
