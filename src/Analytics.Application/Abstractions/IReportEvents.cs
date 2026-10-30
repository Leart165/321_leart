using Analytics.Domain.Reports;

namespace Analytics.Application.Abstractions;

// Ereignisse rund um Berichte. Geschrieben wird in die Outbox, nicht direkt an den Broker: erst
// IUnitOfWork.SaveChangesAsync macht Bericht und Ereignis zusammen gültig. Ob es ein Monatsbericht
// oder ein Jahresauszug ist, sagt der Zeitraum des Berichts.
public interface IReportEvents
{
    void ReportRequested(MonthlyReport report, string correlationId);
}
