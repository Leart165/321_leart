using Analytics.Domain.Exceptions;

namespace Analytics.Domain.Ledger;

public sealed record Currency
{
    private const int CodeLength = 3;

    private Currency(string code)
    {
        Code = code;
    }

    public string Code { get; }

    public static Currency Of(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new InvalidLedgerEventException("Eine Währung muss angegeben sein.");
        }

        string normalised = code.Trim().ToUpperInvariant();
        if (normalised.Length != CodeLength)
        {
            throw new InvalidLedgerEventException($"Die Währung '{code}' hat nicht {CodeLength} Zeichen.");
        }

        foreach (char letter in normalised)
        {
            if (letter < 'A' || letter > 'Z')
            {
                throw new InvalidLedgerEventException($"Die Währung '{code}' enthält Zeichen, die keine Buchstaben sind.");
            }
        }

        return new Currency(normalised);
    }

    public override string ToString()
    {
        return Code;
    }
}
