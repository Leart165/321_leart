using Analytics.Domain.Reports;

namespace Analytics.Api.Dtos;

public sealed record ReportDto(
    Guid ReportId,
    int Year,
    int Month,
    string Status,
    int? BookingCount,
    string? Failure,
    DateTimeOffset RequestedAt,
    DateTimeOffset? CompletedAt)
{
    public static ReportDto From(MonthlyReport report)
    {
        ReportPeriod period = report.Period;
        return new ReportDto(
            report.Id,
            period.Year,
            period.Month,
            report.Status.ToString().ToLowerInvariant(),
            report.BookingCount,
            report.Failure,
            report.RequestedAt,
            report.CompletedAt);
    }
}
