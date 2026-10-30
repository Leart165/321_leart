using Analytics.Application.Ledger;
using Analytics.Domain.Ledger;
using Xunit;

namespace Analytics.Tests.Ledger;

// Summen aus einzelnen Buchungen, so wie sie die Bank liefert: Monate und Tage in UTC, je Währung.
public sealed class LedgerTotalsTests
{
    [Fact]
    public void Months_are_utc_months_per_currency_with_income_expenses_and_count()
    {
        BookedTransaction[] transactions =
        {
            Booked(TransactionKind.Deposit, 100m, "2026-09-30T23:30:00Z"),
            Booked(TransactionKind.TransferIn, 50m, "2026-09-01T00:00:00Z"),
            Booked(TransactionKind.Withdrawal, 30m, "2026-09-15T12:00:00Z"),
            Booked(TransactionKind.TransferOut, 20m, "2026-10-01T00:00:00Z"),
            Booked(TransactionKind.Deposit, 7m, "2026-09-15T12:00:00Z", "EUR")
        };

        IReadOnlyList<MonthlyTotal> months = LedgerTotals.Monthly(transactions);

        Assert.Equal(
            new[]
            {
                new MonthlyTotal(2026, 9, "CHF", 150m, 30m, 3),
                new MonthlyTotal(2026, 9, "EUR", 7m, 0m, 1),
                new MonthlyTotal(2026, 10, "CHF", 0m, 20m, 1)
            },
            months);
    }

    [Fact]
    public void A_transfer_counts_once_in_the_daily_volume()
    {
        BookedTransaction[] transactions =
        {
            Booked(TransactionKind.TransferOut, 20m, "2026-09-17T10:00:00Z"),
            Booked(TransactionKind.TransferIn, 20m, "2026-09-17T10:00:00Z")
        };

        DailyTotal day = Assert.Single(LedgerTotals.Daily(transactions));

        Assert.Equal(new DailyTotal(new DateOnly(2026, 9, 17), "CHF", 20m, 0, 0, 1), day);
    }

    [Fact]
    public void A_booking_from_the_bank_becomes_a_checked_transaction_of_the_customer()
    {
        BookingEntry entry = new BookingEntry(Guid.NewGuid(), "Withdrawal", 12.5m, "CHF", DateTimeOffset.Parse("2026-09-17T10:00:00Z"));

        BookedTransaction transaction = entry.ToTransaction("kunde-1");

        Assert.Equal("kunde-1", transaction.OwnerId);
        Assert.Equal(TransactionKind.Withdrawal, transaction.Kind);
        Assert.Equal(12.5m, transaction.Amount);
    }

    private static BookedTransaction Booked(TransactionKind kind, decimal amount, string bookedAt, string currency = "CHF")
    {
        return BookedTransaction.Of(Guid.NewGuid(), "kunde-1", kind, amount, Currency.Of(currency), DateTimeOffset.Parse(bookedAt));
    }
}
