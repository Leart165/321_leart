using Auth.Domain.Exceptions;

namespace Auth.Domain.Users;

public sealed class User
{
    private User()
    {
        Email = null!;
        PasswordHash = string.Empty;
    }

    private User(Guid id, Email email, string passwordHash, string? displayName, DateTimeOffset registeredAt)
    {
        Id = id;
        Email = email;
        PasswordHash = passwordHash;
        DisplayName = displayName;
        RegisteredAt = registeredAt;
    }

    public Guid Id { get; private set; }

    public Email Email { get; private set; }

    public string PasswordHash { get; private set; }

    public string? DisplayName { get; private set; }

    public DateTimeOffset RegisteredAt { get; private set; }

    public static User Register(Email email, string passwordHash, string? displayName, DateTimeOffset registeredAt)
    {
        ArgumentNullException.ThrowIfNull(email);

        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new DomainValidationException("Ein Benutzer braucht einen Passwort-Hash.");
        }

        string? trimmedName = string.IsNullOrWhiteSpace(displayName) ? null : displayName.Trim();
        if (trimmedName is { Length: > 100 })
        {
            throw new DomainValidationException("Der Anzeigename darf höchstens 100 Zeichen haben.");
        }

        return new User(Guid.NewGuid(), email, passwordHash, trimmedName, registeredAt);
    }
}
