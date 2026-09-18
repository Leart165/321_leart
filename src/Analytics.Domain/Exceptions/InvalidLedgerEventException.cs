namespace Analytics.Domain.Exceptions;

public sealed class InvalidLedgerEventException : Exception
{
    public InvalidLedgerEventException(string message)
        : base(message)
    {
    }
}
