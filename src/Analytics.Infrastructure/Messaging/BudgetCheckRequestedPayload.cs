namespace Analytics.Infrastructure.Messaging;

// Körper von budget.check.requested: wessen Monat zu prüfen ist. Die Beträge liest der
// Konsument aus der eigenen Datenbank, sie stehen nie in der Nachricht.
public sealed record BudgetCheckRequestedPayload(string OwnerId, int Year, int Month, DateTimeOffset RequestedAt);
