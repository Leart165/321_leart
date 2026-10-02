namespace Analytics.Domain.Exceptions;

public sealed class InvalidReportRequestException : Exception
{
    public InvalidReportRequestException(string message)
        : base(message)
    {
    }
}
