namespace Analytics.Infrastructure.Messaging;

// Körper von report.requested nach contracts/analytics/events.asyncapi.v1.yaml. Nur die Id und
// was man zum Lesen im Broker braucht; den Rest liest der Konsument aus der eigenen Datenbank.
public sealed record ReportRequestedPayload(Guid ReportId, int Year, int Month, DateTimeOffset RequestedAt);
