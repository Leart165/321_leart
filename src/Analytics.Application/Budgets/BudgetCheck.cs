namespace Analytics.Application.Budgets;

public enum BudgetCheck
{
    // Eine neue Warnung ist entstanden.
    Raised,

    // Geprüft, keine neue Stufe erreicht oder schon gewarnt.
    Unchanged,

    // Der Kunde hat kein Budget, es gibt nichts zu prüfen.
    NoBudget
}
