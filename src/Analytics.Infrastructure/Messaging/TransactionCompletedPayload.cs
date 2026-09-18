using Analytics.Domain.Exceptions;
using Analytics.Domain.Ledger;
using DomainCurrency = Analytics.Domain.Ledger.Currency;

namespace Analytics.Infrastructure.Messaging;

public sealed record TransactionCompletedPayload
{
    public Guid? TransactionId { get; init; }

    public Guid? AccountId { get; init; }

    public string? OwnerId { get; init; }

    public string? Kind { get; init; }

    public decimal? Amount { get; init; }

    public string? Currency { get; init; }

    public DateTimeOffset? BookedAt { get; init; }

    public BookedTransaction ToBookedTransaction()
    {
        if (TransactionId is null)
        {
            throw new InvalidLedgerEventException("Das Feld transactionId fehlt.");
        }

        if (AccountId is null)
        {
            throw new InvalidLedgerEventException("Das Feld accountId fehlt.");
        }

        if (Amount is null)
        {
            throw new InvalidLedgerEventException("Das Feld amount fehlt.");
        }

        if (BookedAt is null)
        {
            throw new InvalidLedgerEventException("Das Feld bookedAt fehlt.");
        }

        TransactionKind kind = Kind switch
        {
            "Deposit" => TransactionKind.Deposit,
            "Withdrawal" => TransactionKind.Withdrawal,
            "TransferOut" => TransactionKind.TransferOut,
            "TransferIn" => TransactionKind.TransferIn,
            null or "" => throw new InvalidLedgerEventException("Das Feld kind fehlt."),
            _ => throw new InvalidLedgerEventException($"Die Buchungsart '{Kind}' ist unbekannt.")
        };

        return BookedTransaction.Of(
            TransactionId.Value,
            OwnerId ?? string.Empty,
            kind,
            Amount.Value,
            DomainCurrency.Of(Currency ?? string.Empty),
            BookedAt.Value);
    }
}
