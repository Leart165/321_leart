using Analytics.Domain.Exceptions;
using Analytics.Domain.Ledger;

namespace Analytics.Domain.Budgets;

// Das Budget eines Kunden für die Ausgaben eines Monats, in einer Währung. Ausgaben sind, was den
// Saldo vermindert: Auszahlungen und ausgehende Überweisungen, wie bei den Monatssummen.
//
// Ein Kunde hat höchstens ein Budget. Es gilt für jeden Monat, bis er es ändert oder löscht.
public sealed class MonthlyBudget
{
    public const decimal WarningRatio = 0.8m;
    public const decimal MaxLimit = 1_000_000m;

    private const int MaxOwnerIdLength = 100;

    private string _currency;

    // Nur für EF Core. Die Felder werden unmittelbar danach gesetzt.
    private MonthlyBudget()
    {
        OwnerId = string.Empty;
        _currency = string.Empty;
    }

    private MonthlyBudget(string ownerId, Currency currency, decimal limit, DateTimeOffset updatedAt)
    {
        OwnerId = ownerId;
        _currency = currency.Code;
        Limit = limit;
        UpdatedAt = updatedAt;
    }

    public string OwnerId { get; private set; }

    public Currency Currency
    {
        get { return Currency.Of(_currency); }
    }

    public decimal Limit { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static MonthlyBudget Set(string ownerId, Currency currency, decimal limit, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(currency);

        if (string.IsNullOrWhiteSpace(ownerId))
        {
            throw new InvalidBudgetException("Ein Budget braucht einen Inhaber.");
        }

        string trimmed = ownerId.Trim();
        if (trimmed.Length > MaxOwnerIdLength)
        {
            throw new InvalidBudgetException($"Die ownerId darf höchstens {MaxOwnerIdLength} Zeichen haben.");
        }

        CheckLimit(limit);
        return new MonthlyBudget(trimmed, currency, limit, now);
    }

    public void Change(Currency currency, decimal limit, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(currency);
        CheckLimit(limit);

        _currency = currency.Code;
        Limit = limit;
        UpdatedAt = now;
    }

    // Anteil der Ausgaben am Budget, 1 heisst ganz ausgeschöpft.
    public decimal RatioOf(decimal spent)
    {
        return spent / Limit;
    }

    public BudgetLevel LevelFor(decimal spent)
    {
        decimal ratio = RatioOf(spent);
        if (ratio >= 1m)
        {
            return BudgetLevel.Exceeded;
        }

        return ratio >= WarningRatio ? BudgetLevel.Warning : BudgetLevel.Ok;
    }

    private static void CheckLimit(decimal limit)
    {
        if (limit <= 0)
        {
            throw new InvalidBudgetException("Ein Budget muss grösser als null sein.");
        }

        if (limit > MaxLimit)
        {
            throw new InvalidBudgetException($"Ein Budget darf höchstens {MaxLimit:0} betragen.");
        }

        if (decimal.Round(limit, 2) != limit)
        {
            throw new InvalidBudgetException("Ein Budget hat höchstens zwei Nachkommastellen.");
        }
    }
}
