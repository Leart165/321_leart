using Analytics.Domain.Reports;

namespace Analytics.Application.Abstractions;

// Ereignisse rund um Monatsberichte. Geschrieben wird in die Outbox, nicht direkt an den Broker:
// erst IUnitOfWork.SaveChangesAsync macht Bericht und Ereignis zusammen gültig.
public interface IReportEvents
{
    void ReportRequested(MonthlyReport report, string correlationId);
}
