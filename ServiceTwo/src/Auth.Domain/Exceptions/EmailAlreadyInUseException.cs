namespace Auth.Domain.Exceptions;

public sealed class EmailAlreadyInUseException : DomainException
{
    public EmailAlreadyInUseException(string email)
        : base($"Die E-Mail-Adresse '{email}' ist bereits vergeben.")
    {
        Email = email;
    }

    public string Email { get; }
}
