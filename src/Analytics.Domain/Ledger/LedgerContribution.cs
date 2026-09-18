namespace Analytics.Domain.Ledger;

public sealed record LedgerContribution(
    decimal Income,
    decimal Expenses,
    decimal Volume,
    int Deposits,
    int Withdrawals,
    int Transfers)
{
    public static LedgerContribution Of(BookedTransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        decimal amount = transaction.Amount;

        return transaction.Kind switch
        {
            TransactionKind.Deposit => new LedgerContribution(
                Income: amount,
                Expenses: 0m,
                Volume: amount,
                Deposits: 1,
                Withdrawals: 0,
                Transfers: 0),

            TransactionKind.Withdrawal => new LedgerContribution(
                Income: 0m,
                Expenses: amount,
                Volume: amount,
                Deposits: 0,
                Withdrawals: 1,
                Transfers: 0),

            TransactionKind.TransferOut => new LedgerContribution(
                Income: 0m,
                Expenses: amount,
                Volume: amount,
                Deposits: 0,
                Withdrawals: 0,
                Transfers: 1),

            TransactionKind.TransferIn => new LedgerContribution(
                Income: amount,
                Expenses: 0m,
                Volume: 0m,
                Deposits: 0,
                Withdrawals: 0,
                Transfers: 0),

            _ => throw new ArgumentOutOfRangeException(nameof(transaction))
        };
    }
}
