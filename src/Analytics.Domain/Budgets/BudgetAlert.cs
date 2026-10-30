using Analytics.Domain.Ledger;

namespace Analytics.Domain.Budgets;

// Eine Warnung, dass die Ausgaben eines Monats eine Stufe des Budgets erreicht haben. Je Kunde,
// Monat und Stufe gibt es höchstens eine: wer bei 85 Prozent gewarnt wurde, wird bei 90 Prozent
// nicht noch einmal gewarnt, erst wieder bei 100.
public sealed class BudgetAlert
{
    private string _currency;

    // Nur für EF Core. Die Felder werden unmittelbar danach gesetzt.
    private BudgetAlert()
    {
        OwnerId = string.Empty;
        _currency = string.Empty;
    }

    private BudgetAlert(
        Guid id,
        string ownerId,
        int year,
        int month,
        BudgetLevel level,
        decimal spent,
        decimal limit,
        Currency currency,
        DateTimeOffset raisedAt)
    {
        Id = id;
        OwnerId = ownerId;
        Year = year;
        Month = month;
        Level = level;
        Spent = spent;
        Limit = limit;
        _currency = currency.Code;
        RaisedAt = raisedAt;
    }

    public Guid Id { get; private set; }

    public string OwnerId { get; private set; }

    public int Year { get; private set; }

    public int Month { get; private set; }

    public BudgetLevel Level { get; private set; }

    // Ausgaben und Budget im Moment der Warnung.
    public decimal Spent { get; private set; }

    public decimal Limit { get; private set; }

    public Currency Currency
    {
        get { return Currency.Of(_currency); }
    }

    public DateTimeOffset RaisedAt { get; private set; }

    public static BudgetAlert Raise(
        MonthlyBudget budget,
        int year,
        int month,
        BudgetLevel level,
        decimal spent,
        DateTimeOffset raisedAt)
    {
        ArgumentNullException.ThrowIfNull(budget);

        if (level == BudgetLevel.Ok)
        {
            throw new ArgumentOutOfRangeException(nameof(level), "Unter 80 Prozent gibt es keine Warnung.");
        }

        return new BudgetAlert(Guid.NewGuid(), budget.OwnerId, year, month, level, spent, budget.Limit, budget.Currency, raisedAt);
    }
}
