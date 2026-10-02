namespace Analytics.Domain.Exceptions;

public sealed class InvalidReportStateException : Exception
{
    public InvalidReportStateException(Guid reportId, string current, string wanted)
        : base($"Der Monatsbericht {reportId} ist {current} und kann nicht mehr {wanted} werden.")
    {
        ReportId = reportId;
    }

    public Guid ReportId { get; }
}
