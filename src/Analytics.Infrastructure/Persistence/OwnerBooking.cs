namespace Analytics.Infrastructure.Persistence;

// Eine Buchung im Protokoll eines Kunden. Nur was das Partner-Ereignis der Bank enthält: kein
// Buchungstext, kein Konto.
public sealed class OwnerBooking
{
    public Guid TransactionId { get; set; }

    public string OwnerId { get; set; } = string.Empty;

    public string Kind { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public string Currency { get; set; } = string.Empty;

    public DateTimeOffset BookedAt { get; set; }
}
