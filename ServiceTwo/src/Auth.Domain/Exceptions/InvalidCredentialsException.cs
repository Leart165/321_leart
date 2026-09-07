namespace Auth.Domain.Exceptions;

public sealed class InvalidCredentialsException : DomainException
{
    public InvalidCredentialsException()
        : base("E-Mail-Adresse oder Passwort ist falsch.")
    {
    }
}
