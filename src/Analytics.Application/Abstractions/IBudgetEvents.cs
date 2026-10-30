namespace Analytics.Application.Abstractions;

// Ereignisse rund um Budgets. Wie bei den Berichten in die Outbox; erst
// IUnitOfWork.SaveChangesAsync macht sie gültig.
public interface IBudgetEvents
{
    // Die Ausgaben eines Monats sollen mit dem Budget verglichen werden.
    void CheckRequested(string ownerId, int year, int month, string correlationId, DateTimeOffset requestedAt);
}
