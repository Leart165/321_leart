namespace Auth.Domain.Exceptions;

public sealed class UpstreamServiceException : DomainException
{
    public UpstreamServiceException(string message)
        : base(message)
    {
    }
}
