using Analytics.Domain.Exceptions;

namespace Analytics.Domain.Reports;

// Ein Kalendermonat in UTC, wie die Summen und das Buchungsprotokoll.
public sealed record ReportPeriod
{
    public const int MinYear = 2000;
    public const int MaxYear = 2100;

    private ReportPeriod(int year, int month)
    {
        Year = year;
        Month = month;
    }

    public int Year { get; }

    public int Month { get; }

    public DateOnly FirstDay
    {
        get { return new DateOnly(Year, Month, 1); }
    }

    public DateOnly LastDay
    {
        get { return FirstDay.AddMonths(1).AddDays(-1); }
    }

    public static ReportPeriod Of(int year, int month)
    {
        if (year < MinYear || year > MaxYear)
        {
            throw new InvalidReportRequestException($"Das Jahr muss zwischen {MinYear} und {MaxYear} liegen.");
        }

        if (month < 1 || month > 12)
        {
            throw new InvalidReportRequestException("Der Monat muss zwischen 1 und 12 liegen.");
        }

        return new ReportPeriod(year, month);
    }

    public override string ToString()
    {
        return $"{Year:D4}-{Month:D2}";
    }
}
