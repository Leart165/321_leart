using Analytics.Domain.Ledger;

namespace Analytics.Domain.Reports;

// Summe einer Währung im Bericht. Gleiche Begriffe wie MonthlyTotal in openapi.v2.yaml.
public sealed record StatementTotal(
    Currency Currency,
    decimal Income,
    decimal Expenses,
    int Bookings)
{
    public decimal Net
    {
        get { return Income - Expenses; }
    }
}
