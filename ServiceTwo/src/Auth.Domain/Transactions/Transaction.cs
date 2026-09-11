namespace Auth.Domain.Transactions;

public sealed class Transaction
{
    public Guid Id { get; }
    public Guid AccountId { get; }
    public TransactionKind Kind { get; }
    public decimal Amount { get; }
    public string Currency { get; }
    public string? Description { get; }
    public DateTimeOffset BookedAt { get; }

    public Transaction(
        Guid id,
        Guid accountId,
        TransactionKind kind,
        decimal amount,
        string currency,
        string? description,
        DateTimeOffset bookedAt)
    {
        Id = id;
        AccountId = accountId;
        Kind = kind;
        Amount = amount;
        Currency = currency;
        Description = description;
        BookedAt = bookedAt;
    }
}
