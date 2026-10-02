using Analytics.Application.Abstractions;
using Analytics.Application.Ledger;
using Analytics.Domain.Ledger;
using Analytics.Domain.Reports;

namespace Analytics.Application.Reports;

// Alle Vorgänge rund um Monatsberichte. Wie die Überweisung der Bank in zwei Schritten:
//
// RequestAsync läuft in der API. Sie prüft, legt den Bericht an und schreibt das Ereignis
// report.requested in die Outbox, beides in einer Transaktion. Die API antwortet mit 202.
//
// GenerateAsync läuft im Konsumenten der Queue analytics.reports. Sie erzeugt das PDF.
//
// Nur durch diese Trennung nimmt die API weiter Anträge an, während das Erzeugen hängt, und nur
// so teilen sich beide Instanzen die Arbeit.
public sealed class ReportService
{
    // Mehr Buchungen in einem Monat hat kein Kunde; das schützt nur vor einem Riesen-PDF.
    public const int MaxBookings = 10_000;

    public const int ListLimit = 20;

    private readonly IMonthlyReportRepository _reports;
    private readonly IBookingLogReader _bookings;
    private readonly IReportEvents _events;
    private readonly IStatementRenderer _renderer;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _clock;

    public ReportService(
        IMonthlyReportRepository reports,
        IBookingLogReader bookings,
        IReportEvents events,
        IStatementRenderer renderer,
        IUnitOfWork unitOfWork,
        TimeProvider clock)
    {
        _reports = reports;
        _bookings = bookings;
        _events = events;
        _renderer = renderer;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<MonthlyReport> RequestAsync(
        string ownerId,
        int year,
        int month,
        string correlationId,
        CancellationToken cancellationToken)
    {
        MonthlyReport report = MonthlyReport.Request(ownerId, ReportPeriod.Of(year, month), _clock.GetUtcNow());

        _reports.Add(report);
        _events.ReportRequested(report, correlationId);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return report;
    }

    public async Task<MonthlyReport> GetOwnedAsync(Guid reportId, string ownerId, CancellationToken cancellationToken)
    {
        MonthlyReport? report = await _reports.FindAsync(reportId, cancellationToken);
        if (report is null || !report.BelongsTo(ownerId))
        {
            throw new ReportNotFoundException(reportId);
        }

        return report;
    }

    public Task<IReadOnlyList<MonthlyReport>> ListOwnedAsync(string ownerId, CancellationToken cancellationToken)
    {
        return _reports.ListForOwnerAsync(ownerId, ListLimit, cancellationToken);
    }

    public async Task<ReportDocument> GetDocumentAsync(Guid reportId, string ownerId, CancellationToken cancellationToken)
    {
        MonthlyReport report = await GetOwnedAsync(reportId, ownerId, cancellationToken);
        if (report.Status != ReportStatus.Ready)
        {
            throw new ReportNotReadyException(reportId, report.Status);
        }

        byte[] content = await _reports.FindDocumentAsync(reportId, cancellationToken)
            ?? throw new ReportNotReadyException(reportId, report.Status);

        return new ReportDocument(content, _renderer.ContentType, $"buchungen-{report.Period}.pdf");
    }

    // Die Arbeit des Konsumenten. Idempotent: der Broker stellt mindestens einmal zu, ein
    // erledigter Bericht wird übergangen. Bekommen zwei Instanzen dieselbe Nachricht gleichzeitig,
    // gewinnt die erste beim Speichern; die zweite merkt es an ConcurrentChangeException.
    public async Task<ReportGeneration> GenerateAsync(Guid reportId, CancellationToken cancellationToken)
    {
        MonthlyReport? report = await _reports.FindAsync(reportId, cancellationToken);
        if (report is null)
        {
            return ReportGeneration.Unknown;
        }

        if (report.IsCompleted)
        {
            return ReportGeneration.AlreadyCompleted;
        }

        ReportPeriod period = report.Period;
        IReadOnlyList<BookingEntry> entries = await _bookings.GetForOwnerAsync(
            report.OwnerId, period.FirstDay, period.LastDay, MaxBookings + 1, cancellationToken);

        ReportGeneration result;
        if (entries.Count > MaxBookings)
        {
            report.Fail($"Der Monat hat mehr als {MaxBookings} Buchungen.", _clock.GetUtcNow());
            result = ReportGeneration.Failed;
        }
        else
        {
            MonthlyStatement statement = MonthlyStatement.Create(report, entries.Select(ToLine), _clock.GetUtcNow());
            _reports.AddDocument(report.Id, _renderer.Render(statement));
            report.MarkReady(statement.Lines.Count, _clock.GetUtcNow());
            result = ReportGeneration.Generated;
        }

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrentChangeException)
        {
            return ReportGeneration.AlreadyCompleted;
        }

        return result;
    }

    private static StatementLine ToLine(BookingEntry entry)
    {
        return new StatementLine(
            entry.TransactionId,
            Enum.Parse<TransactionKind>(entry.Kind),
            entry.Amount,
            Currency.Of(entry.Currency),
            entry.BookedAt);
    }
}
