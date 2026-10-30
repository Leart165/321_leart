using Analytics.Domain.Ledger;

namespace Analytics.Domain.Reports;

// Eine Zeile im Monatsbericht: genau eine Buchung, so wie die Bank sie liefert: ohne Buchungstext
// und ohne Konto.
public sealed record StatementLine(
    Guid TransactionId,
    TransactionKind Kind,
    decimal Amount,
    Currency Currency,
    DateTimeOffset BookedAt)
{
    // Einzahlungen und eingehende Überweisungen erhöhen den Saldo, alles andere vermindert ihn.
    public bool IsIncome
    {
        get { return Kind is TransactionKind.Deposit or TransactionKind.TransferIn; }
    }

    public decimal SignedAmount
    {
        get { return IsIncome ? Amount : -Amount; }
    }
}
