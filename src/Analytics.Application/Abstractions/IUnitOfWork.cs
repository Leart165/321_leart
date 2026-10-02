namespace Analytics.Application.Abstractions;

public interface IUnitOfWork
{
    // Speichert alle Änderungen in einer Datenbanktransaktion. Hat ein anderer Vorgang denselben
    // Datensatz inzwischen geändert, wirft es ConcurrentChangeException.
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
