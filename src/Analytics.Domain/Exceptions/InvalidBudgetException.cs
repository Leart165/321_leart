namespace Analytics.Domain.Exceptions;

public sealed class InvalidBudgetException : Exception
{
    public InvalidBudgetException(string message)
        : base(message)
    {
    }
}
