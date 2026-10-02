namespace Analytics.Application.Reports;

// Auch für den Bericht eines anderen Inhabers: wer fremde Ids ausprobiert, erfährt nicht,
// dass es sie gibt.
public sealed class ReportNotFoundException : Exception
{
    public ReportNotFoundException(Guid reportId)
        : base($"Den Monatsbericht {reportId} gibt es nicht.")
    {
        ReportId = reportId;
    }

    public Guid ReportId { get; }
}
