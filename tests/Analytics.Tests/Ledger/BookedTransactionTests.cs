using Analytics.Domain.Exceptions;
using Analytics.Domain.Ledger;
using Xunit;

namespace Analytics.Tests.Ledger;

public sealed class BookedTransactionTests
{
    [Fact]
    public void Booking_late_on_the_last_of_september_belongs_to_september()
    {
        BookedTransaction transaction = Booking(DateTimeOffset.Parse("2026-09-30T23:30:00Z"));

        Assert.Equal(2026, transaction.Year);
        Assert.Equal(9, transaction.Month);
        Assert.Equal(new DateOnly(2026, 9, 30), transaction.Day);
    }

    [Fact]
    public void Month_and_day_come_from_utc_not_from_the_local_offset()
    {
        BookedTransaction transaction = Booking(DateTimeOffset.Parse("2026-10-01T01:30:00+02:00"));

        Assert.Equal(2026, transaction.Year);
        Assert.Equal(9, transaction.Month);
        Assert.Equal(new DateOnly(2026, 9, 30), transaction.Day);
    }

    [Fact]
    public void An_amount_of_zero_is_rejected()
    {
        Assert.Throws<InvalidLedgerEventException>(() => BookedTransaction.Of(
            Guid.NewGuid(),
            "owner-1",
            TransactionKind.Deposit,
            0m,
            Currency.Of("CHF"),
            DateTimeOffset.UtcNow));
    }

    [Fact]
    public void A_booking_without_owner_is_rejected()
    {
        Assert.Throws<InvalidLedgerEventException>(() => BookedTransaction.Of(
            Guid.NewGuid(),
            "   ",
            TransactionKind.Deposit,
            10m,
            Currency.Of("CHF"),
            DateTimeOffset.UtcNow));
    }

    private static BookedTransaction Booking(DateTimeOffset bookedAt)
    {
        return BookedTransaction.Of(
            Guid.NewGuid(),
            "owner-1",
            TransactionKind.Deposit,
            100m,
            Currency.Of("CHF"),
            bookedAt);
    }
}
