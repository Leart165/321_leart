using Analytics.Domain.Exceptions;

namespace Analytics.Domain.Reports;

// Der Zeitraum eines Berichts in UTC, wie die Summen und das Buchungsprotokoll: ein Kalendermonat
// für den Monatsbericht oder ein ganzes Kalenderjahr für den Jahresauszug.
public sealed record ReportPeriod
{
    public const int MinYear = 2000;
    public const int MaxYear = 2100;

    private ReportPeriod(int year, int? month)
    {
        Year = year;
        Month = month;
    }

    public int Year { get; }

    // Fehlt beim Jahresauszug.
    public int? Month { get; }

    public bool IsYear
    {
        get { return Month is null; }
    }

    public DateOnly FirstDay
    {
        get { return new DateOnly(Year, Month ?? 1, 1); }
    }

    public DateOnly LastDay
    {
        get { return IsYear ? new DateOnly(Year, 12, 31) : FirstDay.AddMonths(1).AddDays(-1); }
    }

    public static ReportPeriod Of(int year, int month)
    {
        CheckYear(year);

        if (month < 1 || month > 12)
        {
            throw new InvalidReportRequestException("Der Monat muss zwischen 1 und 12 liegen.");
        }

        return new ReportPeriod(year, month);
    }

    public static ReportPeriod OfYear(int year)
    {
        CheckYear(year);
        return new ReportPeriod(year, null);
    }

    // Wie in der Datenbank gespeichert: ein Jahr hat keinen Monat.
    public static ReportPeriod Restore(int year, int? month)
    {
        return month is null ? OfYear(year) : Of(year, month.Value);
    }

    public override string ToString()
    {
        return IsYear ? $"{Year:D4}" : $"{Year:D4}-{Month:D2}";
    }

    private static void CheckYear(int year)
    {
        if (year < MinYear || year > MaxYear)
        {
            throw new InvalidReportRequestException($"Das Jahr muss zwischen {MinYear} und {MaxYear} liegen.");
        }
    }
}
