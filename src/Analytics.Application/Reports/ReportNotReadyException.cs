using Analytics.Domain.Reports;

namespace Analytics.Application.Reports;

public sealed class ReportNotReadyException : Exception
{
    public ReportNotReadyException(Guid reportId, ReportStatus status)
        : base(status == ReportStatus.Failed
            ? $"Der Monatsbericht {reportId} ist gescheitert, es gibt kein PDF."
            : $"Der Monatsbericht {reportId} wird noch erstellt.")
    {
        ReportId = reportId;
        Status = status;
    }

    public Guid ReportId { get; }

    public ReportStatus Status { get; }
}
