namespace Auth.Infrastructure.AccountsApi;

internal sealed class AccountApiDto
{
    public Guid Id { get; set; }
    public string OwnerId { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;
    public decimal Balance { get; set; }
    public DateTimeOffset OpenedAt { get; set; }
}

internal sealed class TransactionApiDto
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public string Kind { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTimeOffset BookedAt { get; set; }
}
