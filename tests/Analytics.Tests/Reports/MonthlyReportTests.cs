using Analytics.Domain.Exceptions;
using Analytics.Domain.Reports;
using Xunit;

namespace Analytics.Tests.Reports;

// Der Monatsbericht als Aggregat: was beantragt werden darf und welche Übergänge erlaubt sind.
public sealed class MonthlyReportTests
{
    private static readonly DateTimeOffset Now = new DateTimeOffset(2026, 10, 2, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_new_report_is_requested_and_belongs_to_its_owner()
    {
        MonthlyReport report = MonthlyReport.Request(" kunde-1 ", ReportPeriod.Of(2026, 9), Now);

        Assert.Equal(ReportStatus.Requested, report.Status);
        Assert.Equal("kunde-1", report.OwnerId);
        Assert.True(report.BelongsTo("kunde-1"));
        Assert.False(report.BelongsTo("kunde-2"));
        Assert.False(report.IsCompleted);
        Assert.Null(report.CompletedAt);
    }

    [Fact]
    public void The_current_month_may_be_requested_but_not_a_future_one()
    {
        Assert.Equal(ReportStatus.Requested, MonthlyReport.Request("kunde-1", ReportPeriod.Of(2026, 10), Now).Status);
        Assert.Throws<InvalidReportRequestException>(() => MonthlyReport.Request("kunde-1", ReportPeriod.Of(2026, 11), Now));
    }

    [Theory]
    [InlineData(2026, 0)]
    [InlineData(2026, 13)]
    [InlineData(1999, 12)]
    [InlineData(2101, 1)]
    public void A_period_outside_the_allowed_range_is_rejected(int year, int month)
    {
        Assert.Throws<InvalidReportRequestException>(() => ReportPeriod.Of(year, month));
    }

    [Fact]
    public void A_period_covers_the_whole_month_also_in_a_leap_year()
    {
        ReportPeriod february = ReportPeriod.Of(2028, 2);

        Assert.Equal(new DateOnly(2028, 2, 1), february.FirstDay);
        Assert.Equal(new DateOnly(2028, 2, 29), february.LastDay);
        Assert.Equal("2028-02", february.ToString());
    }

    [Fact]
    public void A_report_without_owner_is_rejected()
    {
        Assert.Throws<InvalidReportRequestException>(() => MonthlyReport.Request(" ", ReportPeriod.Of(2026, 9), Now));
    }

    [Fact]
    public void A_ready_report_keeps_its_booking_count_and_cannot_change_again()
    {
        MonthlyReport report = MonthlyReport.Request("kunde-1", ReportPeriod.Of(2026, 9), Now);

        report.MarkReady(12, Now.AddSeconds(1));

        Assert.Equal(ReportStatus.Ready, report.Status);
        Assert.Equal(12, report.BookingCount);
        Assert.Equal(Now.AddSeconds(1), report.CompletedAt);
        Assert.Throws<InvalidReportStateException>(() => report.MarkReady(12, Now));
        Assert.Throws<InvalidReportStateException>(() => report.Fail("zu spät", Now));
    }

    [Fact]
    public void A_failed_report_keeps_its_reason()
    {
        MonthlyReport report = MonthlyReport.Request("kunde-1", ReportPeriod.Of(2026, 9), Now);

        report.Fail("  zu viele Buchungen ", Now);

        Assert.Equal(ReportStatus.Failed, report.Status);
        Assert.Equal("zu viele Buchungen", report.Failure);
        Assert.Throws<InvalidReportStateException>(() => report.MarkReady(1, Now));
    }
}
