using Analytics.Domain.Exceptions;

namespace Analytics.Domain.Reports;

// Ein beantragter Monatsbericht als PDF. Eigenes Aggregat: er hält, was beantragt wurde und wie
// es ausgegangen ist, nicht das PDF selbst.
//
// Die API legt ihn im Zustand Requested an und antwortet sofort mit 202. Erzeugt wird das PDF
// danach vom Konsumenten der Queue analytics.reports; erst er setzt Ready oder Failed.
public sealed class MonthlyReport
{
    private const int MaxOwnerIdLength = 100;
    private const int MaxFailureLength = 500;

    private int _year;
    private int _month;

    // Nur für EF Core. Die Felder werden unmittelbar danach gesetzt.
    private MonthlyReport()
    {
        OwnerId = string.Empty;
    }

    private MonthlyReport(
        Guid id,
        string ownerId,
        ReportPeriod period,
        ReportStatus status,
        int? bookingCount,
        string? failure,
        DateTimeOffset requestedAt,
        DateTimeOffset? completedAt)
    {
        Id = id;
        OwnerId = ownerId;
        _year = period.Year;
        _month = period.Month;
        Status = status;
        BookingCount = bookingCount;
        Failure = failure;
        RequestedAt = requestedAt;
        CompletedAt = completedAt;
    }

    public Guid Id { get; private set; }

    public string OwnerId { get; private set; }

    public ReportPeriod Period
    {
        get { return ReportPeriod.Of(_year, _month); }
    }

    public ReportStatus Status { get; private set; }

    // Wie viele Buchungen im PDF stehen, nur bei Ready gesetzt.
    public int? BookingCount { get; private set; }

    // Grund des Scheiterns, nur bei Failed gesetzt.
    public string? Failure { get; private set; }

    public DateTimeOffset RequestedAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public bool IsCompleted
    {
        get { return Status != ReportStatus.Requested; }
    }

    public static MonthlyReport Request(string ownerId, ReportPeriod period, DateTimeOffset requestedAt)
    {
        ArgumentNullException.ThrowIfNull(period);

        if (string.IsNullOrWhiteSpace(ownerId))
        {
            throw new InvalidReportRequestException("Ein Monatsbericht braucht einen Inhaber.");
        }

        string trimmedOwnerId = ownerId.Trim();
        if (trimmedOwnerId.Length > MaxOwnerIdLength)
        {
            throw new InvalidReportRequestException($"Die ownerId darf höchstens {MaxOwnerIdLength} Zeichen haben.");
        }

        // Ein Monat, der noch nicht begonnen hat, hat keine Buchungen.
        DateOnly today = DateOnly.FromDateTime(requestedAt.UtcDateTime);
        if (period.FirstDay > today)
        {
            throw new InvalidReportRequestException($"Der Monat {period} liegt in der Zukunft.");
        }

        return new MonthlyReport(
            Guid.NewGuid(), trimmedOwnerId, period, ReportStatus.Requested,
            bookingCount: null, failure: null, requestedAt, completedAt: null);
    }

    // Wiederherstellung, etwa in Tests. Prüft nichts: was gespeichert ist, war beim Speichern gültig.
    public static MonthlyReport Restore(
        Guid id,
        string ownerId,
        ReportPeriod period,
        ReportStatus status,
        int? bookingCount,
        string? failure,
        DateTimeOffset requestedAt,
        DateTimeOffset? completedAt)
    {
        return new MonthlyReport(id, ownerId, period, status, bookingCount, failure, requestedAt, completedAt);
    }

    public bool BelongsTo(string ownerId)
    {
        return string.Equals(OwnerId, ownerId, StringComparison.Ordinal);
    }

    public void MarkReady(int bookingCount, DateTimeOffset completedAt)
    {
        EnsureRequested(nameof(ReportStatus.Ready));

        if (bookingCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bookingCount), "Die Anzahl Buchungen kann nicht negativ sein.");
        }

        Status = ReportStatus.Ready;
        BookingCount = bookingCount;
        CompletedAt = completedAt;
    }

    public void Fail(string reason, DateTimeOffset completedAt)
    {
        EnsureRequested(nameof(ReportStatus.Failed));

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Ein gescheiterter Bericht braucht einen Grund.", nameof(reason));
        }

        string trimmed = reason.Trim();
        Status = ReportStatus.Failed;
        Failure = trimmed.Length > MaxFailureLength ? trimmed[..MaxFailureLength] : trimmed;
        CompletedAt = completedAt;
    }

    private void EnsureRequested(string wanted)
    {
        if (Status != ReportStatus.Requested)
        {
            throw new InvalidReportStateException(Id, Status.ToString(), wanted);
        }
    }
}
