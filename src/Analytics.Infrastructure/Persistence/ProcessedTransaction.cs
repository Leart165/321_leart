namespace Analytics.Infrastructure.Persistence;

public sealed class ProcessedTransaction
{
    public Guid TransactionId { get; set; }

    public DateTimeOffset ProcessedAt { get; set; }
}
