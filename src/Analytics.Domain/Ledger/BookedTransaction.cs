using Analytics.Domain.Exceptions;

namespace Analytics.Domain.Ledger;

public sealed class BookedTransaction
{
    private const int MaxOwnerIdLength = 100;

    private BookedTransaction(
        Guid transactionId,
        string ownerId,
        TransactionKind kind,
        decimal amount,
        Currency currency,
        DateTimeOffset bookedAt)
    {
        TransactionId = transactionId;
        OwnerId = ownerId;
        Kind = kind;
        Amount = amount;
        Currency = currency;
        BookedAt = bookedAt;
    }

    public Guid TransactionId { get; }

    public string OwnerId { get; }

    public TransactionKind Kind { get; }

    public decimal Amount { get; }

    public Currency Currency { get; }

    public DateTimeOffset BookedAt { get; }

    public int Year
    {
        get { return BookedAt.UtcDateTime.Year; }
    }

    public int Month
    {
        get { return BookedAt.UtcDateTime.Month; }
    }

    public DateOnly Day
    {
        get { return DateOnly.FromDateTime(BookedAt.UtcDateTime); }
    }

    public static BookedTransaction Of(
        Guid transactionId,
        string ownerId,
        TransactionKind kind,
        decimal amount,
        Currency currency,
        DateTimeOffset bookedAt)
    {
        ArgumentNullException.ThrowIfNull(currency);

        if (transactionId == Guid.Empty)
        {
            throw new InvalidLedgerEventException("Eine Buchung braucht eine transactionId.");
        }

        if (string.IsNullOrWhiteSpace(ownerId))
        {
            throw new InvalidLedgerEventException("Eine Buchung braucht eine ownerId.");
        }

        string trimmedOwnerId = ownerId.Trim();
        if (trimmedOwnerId.Length > MaxOwnerIdLength)
        {
            throw new InvalidLedgerEventException($"Die ownerId darf höchstens {MaxOwnerIdLength} Zeichen haben.");
        }

        if (!Enum.IsDefined(kind))
        {
            throw new InvalidLedgerEventException($"Die Buchungsart '{kind}' ist unbekannt.");
        }

        if (amount <= 0m)
        {
            throw new InvalidLedgerEventException("Ein Buchungsbetrag muss grösser als null sein.");
        }

        return new BookedTransaction(transactionId, trimmedOwnerId, kind, amount, currency, bookedAt);
    }
}
