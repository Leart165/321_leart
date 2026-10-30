namespace Analytics.Infrastructure.Messaging;

// Körper von report.requested nach contracts/analytics/events.asyncapi.v1.yaml. Nur die Id und
// was man zum Lesen im Broker braucht; den Rest liest der Konsument aus der eigenen Datenbank.
public sealed record ReportRequestedPayload(Guid ReportId, int Year, int Month, DateTimeOffset RequestedAt);

// Körper von annual-report.requested: wie oben, aber für ein ganzes Jahr.
public sealed record AnnualReportRequestedPayload(Guid ReportId, int Year, DateTimeOffset RequestedAt);
