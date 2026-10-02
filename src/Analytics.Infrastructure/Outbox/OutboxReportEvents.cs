using Analytics.Application.Abstractions;
using Analytics.Domain.Reports;
using Analytics.Infrastructure.Messaging;
using Analytics.Infrastructure.Persistence;
using System.Diagnostics;
using System.Text.Json;

namespace Analytics.Infrastructure.Outbox;

// Schreibt report.requested in die Outbox, im selben DbContext wie den Bericht. Publiziert wird
// erst, wenn beides gespeichert ist; vorher weiss der Broker nichts davon.
public sealed class OutboxReportEvents : IReportEvents
{
    private const int SchemaVersion = 1;

    private static readonly JsonSerializerOptions SerializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);

    private readonly AnalyticsDbContext _context;

    public OutboxReportEvents(AnalyticsDbContext context)
    {
        _context = context;
    }

    public void ReportRequested(MonthlyReport report, string correlationId)
    {
        ReportPeriod period = report.Period;
        string payload = JsonSerializer.Serialize(
            new ReportRequestedPayload(report.Id, period.Year, period.Month, report.RequestedAt),
            SerializerOptions);

        _context.Outbox.Add(OutboxMessage.Create(
            ReportTopology.ReportRequestedRoutingKey,
            ReportTopology.ReportRequestedType,
            SchemaVersion,
            correlationId,
            Activity.Current?.Id,
            payload,
            report.RequestedAt));
    }
}
