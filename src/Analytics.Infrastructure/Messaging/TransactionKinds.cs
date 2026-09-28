using Analytics.Domain.Exceptions;
using Analytics.Domain.Ledger;

namespace Analytics.Infrastructure.Messaging;

// Die Buchungsarten stehen in beiden Ereignissen der Bank gleich geschrieben. Bewusst ohne
// Enum.Parse: eine unbekannte Art ist ein Fehler im Ereignis, keine Zahl, die zufällig passt.
public static class TransactionKinds
{
    public static TransactionKind Parse(string? kind)
    {
        return kind switch
        {
            "Deposit" => TransactionKind.Deposit,
            "Withdrawal" => TransactionKind.Withdrawal,
            "TransferOut" => TransactionKind.TransferOut,
            "TransferIn" => TransactionKind.TransferIn,
            null or "" => throw new InvalidLedgerEventException("Das Feld kind fehlt."),
            _ => throw new InvalidLedgerEventException($"Die Buchungsart '{kind}' ist unbekannt.")
        };
    }
}
