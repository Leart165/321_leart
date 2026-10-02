using Analytics.Domain.Exceptions;
using Analytics.Domain.Ledger;
using DomainCurrency = Analytics.Domain.Ledger.Currency;

namespace Analytics.Infrastructure.Messaging;

// partner.transaction.completed nach contracts/partner/asyncapi.v1.yaml: ohne Konto, ohne
// Buchungstext, ohne Überweisung. Mehr braucht die Auswertung nicht.
public sealed record PartnerTransactionCompletedPayload
{
    public Guid? TransactionId { get; init; }

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

        if (Amount is null)
        {
            throw new InvalidLedgerEventException("Das Feld amount fehlt.");
        }

        if (BookedAt is null)
        {
            throw new InvalidLedgerEventException("Das Feld bookedAt fehlt.");
        }

        return BookedTransaction.Of(
            TransactionId.Value,
            OwnerId ?? string.Empty,
            TransactionKinds.Parse(Kind),
            Amount.Value,
            DomainCurrency.Of(Currency ?? string.Empty),
            BookedAt.Value);
    }
}
