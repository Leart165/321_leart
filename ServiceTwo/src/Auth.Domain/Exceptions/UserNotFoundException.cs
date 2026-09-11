namespace Auth.Domain.Exceptions;

public sealed class UserNotFoundException : DomainException
{
    public UserNotFoundException(Guid userId)
        : base($"Der Benutzer {userId} existiert nicht.")
    {
        UserId = userId;
    }

    public Guid UserId { get; }
}
