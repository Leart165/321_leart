using Analytics.Application.Abstractions;
using Analytics.Domain.Reports;
using Analytics.Infrastructure.Messaging;
using Analytics.Infrastructure.Persistence;
using System.Diagnostics;
using System.Text.Json;

namespace Analytics.Infrastructure.Outbox;

// Schreibt report.requested oder annual-report.requested in die Outbox, im selben DbContext wie
// den Bericht. Publiziert wird
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

        // Der Jahresauszug geht an seine eigene Queue: anderes Ereignis, anderer Routing Key.
        (string routingKey, string type, string payload) = period.IsYear
            ? (ReportTopology.AnnualReportRequestedRoutingKey,
                ReportTopology.AnnualReportRequestedType,
                JsonSerializer.Serialize(
                    new AnnualReportRequestedPayload(report.Id, period.Year, report.RequestedAt), SerializerOptions))
            : (ReportTopology.ReportRequestedRoutingKey,
                ReportTopology.ReportRequestedType,
                JsonSerializer.Serialize(
                    new ReportRequestedPayload(report.Id, period.Year, period.Month!.Value, report.RequestedAt), SerializerOptions));

        _context.Outbox.Add(OutboxMessage.Create(
            routingKey,
            type,
            SchemaVersion,
            correlationId,
            Activity.Current?.Id,
            payload,
            report.RequestedAt));
    }
}
