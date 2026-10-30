namespace Analytics.Infrastructure.Persistence;

// Eine Buchung eines Kunden, wie sie die Bank mit seiner Zustimmung geliefert hat
// (GET /v2/statements/bookings): kein Buchungstext, kein Konto. Der gespeicherte Stand für einen
// Ausfall der Bank, für die Monatsberichte und die Tagessummen; gelöscht, sobald die Zustimmung
// fehlt.
public sealed class OwnerBooking
{
    public Guid TransactionId { get; set; }

    public string OwnerId { get; set; } = string.Empty;

    public string Kind { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public string Currency { get; set; } = string.Empty;

    public DateTimeOffset BookedAt { get; set; }
}
