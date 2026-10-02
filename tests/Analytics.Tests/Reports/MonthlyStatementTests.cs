using Analytics.Domain.Ledger;
using Analytics.Domain.Reports;
using Xunit;

namespace Analytics.Tests.Reports;

// Inhalt des Berichts: jede Buchung des Monats in zeitlicher Reihenfolge, Summen je Währung.
public sealed class MonthlyStatementTests
{
    private static readonly DateTimeOffset Now = new DateTimeOffset(2026, 10, 2, 9, 0, 0, TimeSpan.Zero);
    private static readonly Currency Chf = Currency.Of("CHF");
    private static readonly Currency Eur = Currency.Of("EUR");

    [Fact]
    public void Bookings_are_listed_oldest_first_and_only_from_the_month()
    {
        StatementLine late = Line(TransactionKind.Withdrawal, 20m, Chf, At(30, 23));
        StatementLine early = Line(TransactionKind.Deposit, 100m, Chf, At(1, 0));
        StatementLine before = Line(TransactionKind.Deposit, 999m, Chf, new DateTimeOffset(2026, 8, 31, 23, 59, 59, TimeSpan.Zero));
        StatementLine after = Line(TransactionKind.Deposit, 999m, Chf, new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero));

        MonthlyStatement statement = MonthlyStatement.Create(Report(), new[] { late, before, early, after }, Now);

        Assert.Equal(new[] { early, late }, statement.Lines);
    }

    [Fact]
    public void Totals_are_kept_per_currency_with_income_expenses_and_net()
    {
        MonthlyStatement statement = MonthlyStatement.Create(Report(), new[]
        {
            Line(TransactionKind.Deposit, 100.10m, Chf, At(2, 8)),
            Line(TransactionKind.TransferIn, 0.20m, Chf, At(3, 8)),
            Line(TransactionKind.Withdrawal, 30m, Chf, At(4, 8)),
            Line(TransactionKind.TransferOut, 0.30m, Chf, At(5, 8)),
            Line(TransactionKind.Deposit, 5m, Eur, At(6, 8))
        }, Now);

        Assert.Equal(new[] { "CHF", "EUR" }, statement.Totals.Select(total => total.Currency.Code));
        StatementTotal chf = statement.Totals[0];
        Assert.Equal(100.30m, chf.Income);
        Assert.Equal(30.30m, chf.Expenses);
        Assert.Equal(70.00m, chf.Net);
        Assert.Equal(4, chf.Bookings);
    }

    [Fact]
    public void Income_is_positive_and_expenses_are_negative()
    {
        Assert.Equal(5m, Line(TransactionKind.Deposit, 5m, Chf, At(1, 1)).SignedAmount);
        Assert.Equal(5m, Line(TransactionKind.TransferIn, 5m, Chf, At(1, 1)).SignedAmount);
        Assert.Equal(-5m, Line(TransactionKind.Withdrawal, 5m, Chf, At(1, 1)).SignedAmount);
        Assert.Equal(-5m, Line(TransactionKind.TransferOut, 5m, Chf, At(1, 1)).SignedAmount);
    }

    [Fact]
    public void An_empty_month_has_no_lines_and_no_totals()
    {
        MonthlyStatement statement = MonthlyStatement.Create(Report(), Array.Empty<StatementLine>(), Now);

        Assert.Empty(statement.Lines);
        Assert.Empty(statement.Totals);
        Assert.Equal("kunde-1", statement.OwnerId);
    }

    public static MonthlyReport Report()
    {
        return MonthlyReport.Request("kunde-1", ReportPeriod.Of(2026, 9), Now);
    }

    public static StatementLine Line(TransactionKind kind, decimal amount, Currency currency, DateTimeOffset bookedAt)
    {
        return new StatementLine(Guid.NewGuid(), kind, amount, currency, bookedAt);
    }

    public static DateTimeOffset At(int day, int hour)
    {
        return new DateTimeOffset(2026, 9, day, hour, 0, 0, TimeSpan.Zero);
    }
}
