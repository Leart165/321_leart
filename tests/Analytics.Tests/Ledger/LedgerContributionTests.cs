using Analytics.Domain.Ledger;
using Xunit;

namespace Analytics.Tests.Ledger;

public sealed class LedgerContributionTests
{
    [Fact]
    public void Deposit_counts_as_income_and_into_volume()
    {
        LedgerContribution contribution = LedgerContribution.Of(Booking(TransactionKind.Deposit, 100m));

        Assert.Equal(100m, contribution.Income);
        Assert.Equal(0m, contribution.Expenses);
        Assert.Equal(100m, contribution.Volume);
        Assert.Equal(1, contribution.Deposits);
        Assert.Equal(0, contribution.Withdrawals);
        Assert.Equal(0, contribution.Transfers);
    }

    [Fact]
    public void Withdrawal_counts_as_expense_and_into_volume()
    {
        LedgerContribution contribution = LedgerContribution.Of(Booking(TransactionKind.Withdrawal, 30m));

        Assert.Equal(0m, contribution.Income);
        Assert.Equal(30m, contribution.Expenses);
        Assert.Equal(30m, contribution.Volume);
        Assert.Equal(0, contribution.Deposits);
        Assert.Equal(1, contribution.Withdrawals);
        Assert.Equal(0, contribution.Transfers);
    }

    [Fact]
    public void TransferOut_counts_as_expense_into_volume_and_as_the_transfer()
    {
        LedgerContribution contribution = LedgerContribution.Of(Booking(TransactionKind.TransferOut, 20m));

        Assert.Equal(0m, contribution.Income);
        Assert.Equal(20m, contribution.Expenses);
        Assert.Equal(20m, contribution.Volume);
        Assert.Equal(1, contribution.Transfers);
    }

    [Fact]
    public void TransferIn_counts_as_income_but_never_into_volume_or_transfers()
    {
        LedgerContribution contribution = LedgerContribution.Of(Booking(TransactionKind.TransferIn, 50m));

        Assert.Equal(50m, contribution.Income);
        Assert.Equal(0m, contribution.Expenses);
        Assert.Equal(0m, contribution.Volume);
        Assert.Equal(0, contribution.Transfers);
    }

    private static BookedTransaction Booking(TransactionKind kind, decimal amount)
    {
        return BookedTransaction.Of(
            Guid.NewGuid(),
            "owner-1",
            kind,
            amount,
            Currency.Of("CHF"),
            DateTimeOffset.Parse("2026-09-17T09:30:00Z"));
    }
}
