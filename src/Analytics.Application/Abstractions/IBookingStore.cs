using Analytics.Application.Ledger;

namespace Analytics.Application.Abstractions;

// Was die Bank zu einem Kunden geliefert hat, mit seiner Zustimmung gespeichert. Doppelte zählen
// einmal: die transactionId ist der Schlüssel.
public interface IBookingStore
{
    // Vormerken; geschrieben wird mit IUnitOfWork.SaveChangesAsync, in derselben Transaktion wie
    // alles andere.
    void Stage(string ownerId, IEnumerable<BookingEntry> bookings);

    // Sofort und in einem eigenen Vorgang. Gibt zurück, wie viele Buchungen neu waren. Ist die
    // eigene Datenbank gerade weg, gibt es 0 zurück, statt zu werfen: das Speichern ist ein
    // Vorrat, die Antwort hängt nicht daran.
    Task<int> TryRememberAsync(string ownerId, IReadOnlyCollection<BookingEntry> bookings, CancellationToken cancellationToken);

    // Löscht alles, was von diesem Kunden aus Buchungen gespeichert ist, auch die Warnungen zum
    // Budget; das Budget selbst ist eine Einstellung des Kunden und bleibt. Wie TryRememberAsync ohne zu werfen,
    // wenn die Datenbank weg ist; der nächste Aufruf ohne Zustimmung löscht dann.
    Task<bool> TryForgetAsync(string ownerId, CancellationToken cancellationToken);

    // Ob von diesem Kunden überhaupt etwas gespeichert ist.
    Task<bool> KnowsAsync(string ownerId, CancellationToken cancellationToken);
}
