namespace Analytics.Domain.Exceptions;

public sealed class InvalidReportStateException : Exception
{
    public InvalidReportStateException(Guid reportId, string current, string wanted)
        : base($"Der Bericht {reportId} ist {current} und kann nicht mehr {wanted} werden.")
    {
        ReportId = reportId;
    }

    public Guid ReportId { get; }
}
