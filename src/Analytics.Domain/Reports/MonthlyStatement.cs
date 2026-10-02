namespace Analytics.Domain.Reports;

// Inhalt eines Monatsberichts, unabhängig davon, wie er gedruckt wird: jede Buchung des Monats
// in zeitlicher Reihenfolge, wie auf einem Kontoauszug, und die Summen je Währung.
public sealed class MonthlyStatement
{
    private MonthlyStatement(
        string ownerId,
        ReportPeriod period,
        IReadOnlyList<StatementLine> lines,
        IReadOnlyList<StatementTotal> totals,
        DateTimeOffset generatedAt)
    {
        OwnerId = ownerId;
        Period = period;
        Lines = lines;
        Totals = totals;
        GeneratedAt = generatedAt;
    }

    public string OwnerId { get; }

    public ReportPeriod Period { get; }

    // Älteste zuerst; bei gleichem Zeitpunkt nach transactionId, damit zwei Ausdrucke gleich sind.
    public IReadOnlyList<StatementLine> Lines { get; }

    // Je Währung eine Summe, alphabetisch.
    public IReadOnlyList<StatementTotal> Totals { get; }

    public DateTimeOffset GeneratedAt { get; }

    public static MonthlyStatement Create(
        MonthlyReport report,
        IEnumerable<StatementLine> bookings,
        DateTimeOffset generatedAt)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(bookings);

        ReportPeriod period = report.Period;
        List<StatementLine> lines = bookings
            .Where(line => BelongsTo(period, line))
            .OrderBy(line => line.BookedAt)
            .ThenBy(line => line.TransactionId)
            .ToList();

        List<StatementTotal> totals = lines
            .GroupBy(line => line.Currency)
            .Select(group => new StatementTotal(
                group.Key,
                Income: group.Where(line => line.IsIncome).Sum(line => line.Amount),
                Expenses: group.Where(line => !line.IsIncome).Sum(line => line.Amount),
                Bookings: group.Count()))
            .OrderBy(total => total.Currency.Code, StringComparer.Ordinal)
            .ToList();

        return new MonthlyStatement(report.OwnerId, period, lines, totals, generatedAt);
    }

    private static bool BelongsTo(ReportPeriod period, StatementLine line)
    {
        DateOnly day = DateOnly.FromDateTime(line.BookedAt.UtcDateTime);
        return day >= period.FirstDay && day <= period.LastDay;
    }
}
