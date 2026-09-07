using Auth.Domain.Exceptions;
using System.Text.RegularExpressions;

namespace Auth.Domain.Users;

public sealed partial class Email : IEquatable<Email>
{
    private const int MaxLength = 200;

    private Email(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static Email Of(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainValidationException("Eine E-Mail-Adresse ist erforderlich.");
        }

        string trimmed = value.Trim();
        if (trimmed.Length > MaxLength)
        {
            throw new DomainValidationException($"Die E-Mail-Adresse darf höchstens {MaxLength} Zeichen haben.");
        }

        if (!Pattern().IsMatch(trimmed))
        {
            throw new DomainValidationException("Die E-Mail-Adresse ist ungültig.");
        }

        return new Email(trimmed.ToLowerInvariant());
    }

    public bool Equals(Email? other)
    {
        return other is not null && Value == other.Value;
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as Email);
    }

    public override int GetHashCode()
    {
        return Value.GetHashCode();
    }

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex Pattern();
}
