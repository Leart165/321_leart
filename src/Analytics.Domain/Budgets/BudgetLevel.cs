namespace Analytics.Domain.Budgets;

// Wie weit die Ausgaben eines Monats das Budget ausschöpfen. Im Kontrakt klein geschrieben.
public enum BudgetLevel
{
    // Unter 80 Prozent.
    Ok,

    // Ab 80 Prozent: bald ist das Budget aufgebraucht.
    Warning,

    // Ab 100 Prozent: mehr ausgegeben als geplant.
    Exceeded
}
