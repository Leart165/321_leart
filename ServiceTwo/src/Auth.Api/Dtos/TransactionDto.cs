using Auth.Domain.Transactions;

namespace Auth.Api.Dtos;

public sealed record TransactionDto(
    Guid Id,
    Guid AccountId,
    string Kind,
    decimal Amount,
    string Currency,
    string? Description,
    DateTimeOffset BookedAt)
{
    public static TransactionDto From(Transaction transaction) => new(
        transaction.Id,
        transaction.AccountId,
        transaction.Kind.ToString(),
        transaction.Amount,
        transaction.Currency,
        transaction.Description,
        transaction.BookedAt);
}
