namespace Analytics.Application.Ledger;

public sealed record BookingEntry(
    Guid TransactionId,
    string Kind,
    decimal Amount,
    string Currency,
    DateTimeOffset BookedAt);
