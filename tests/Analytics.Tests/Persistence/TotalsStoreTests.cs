using Analytics.Application.Ledger;
using Analytics.Domain.Ledger;
using Analytics.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Analytics.Tests.Persistence;

public sealed class TotalsStoreTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private const string Owner = "owner-1";

    private readonly PostgresFixture _postgres;

    public TotalsStoreTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    public Task InitializeAsync()
    {
        return _postgres.ResetAsync();
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Income_and_expenses_add_up_across_the_kinds()
    {
        await ApplyAsync(Booking(TransactionKind.Deposit, 100m));
        await ApplyAsync(Booking(TransactionKind.TransferIn, 50m));
        await ApplyAsync(Booking(TransactionKind.Withdrawal, 30m));
        await ApplyAsync(Booking(TransactionKind.TransferOut, 20m));

        OwnerMonthly monthly = await SingleMonthlyAsync();

        Assert.Equal(150m, monthly.Income);
        Assert.Equal(50m, monthly.Expenses);
        Assert.Equal(100m, monthly.Income - monthly.Expenses);
        Assert.Equal(4, monthly.Transactions);
    }

    [Fact]
    public async Task A_transfer_raises_the_volume_once_and_counts_as_one_transfer()
    {
        await ApplyAsync(Booking(TransactionKind.TransferOut, 20m));
        await ApplyAsync(Booking(TransactionKind.TransferIn, 20m));

        SystemDaily daily = await SingleDailyAsync();

        Assert.Equal(20m, daily.Volume);
        Assert.Equal(1, daily.Transfers);
        Assert.Equal(0, daily.Deposits);
        Assert.Equal(0, daily.Withdrawals);
    }

    [Fact]
    public async Task The_same_transaction_is_counted_only_once()
    {
        BookedTransaction booking = Booking(TransactionKind.Deposit, 100m);

        bool first = await ApplyAsync(booking);
        bool second = await ApplyAsync(booking);

        Assert.True(first);
        Assert.False(second);

        OwnerMonthly monthly = await SingleMonthlyAsync();
        Assert.Equal(100m, monthly.Income);
        Assert.Equal(1, monthly.Transactions);

        SystemDaily daily = await SingleDailyAsync();
        Assert.Equal(100m, daily.Volume);
        Assert.Equal(1, daily.Deposits);
    }

    [Fact]
    public async Task A_booking_late_on_the_last_of_september_lands_in_september()
    {
        await ApplyAsync(Booking(
            TransactionKind.Deposit,
            100m,
            DateTimeOffset.Parse("2026-09-30T23:30:00Z")));

        OwnerMonthly monthly = await SingleMonthlyAsync();
        Assert.Equal(2026, monthly.Year);
        Assert.Equal(9, monthly.Month);

        SystemDaily daily = await SingleDailyAsync();
        Assert.Equal(new DateOnly(2026, 9, 30), daily.Day);
    }

    private async Task<bool> ApplyAsync(BookedTransaction transaction)
    {
        await using AnalyticsDbContext context = _postgres.CreateContext();
        LedgerProjection projection = new LedgerProjection(new TotalsStore(context));
        return await projection.ApplyAsync(transaction, CancellationToken.None);
    }

    private async Task<OwnerMonthly> SingleMonthlyAsync()
    {
        await using AnalyticsDbContext context = _postgres.CreateContext();
        return await context.OwnerMonthly.SingleAsync();
    }

    private async Task<SystemDaily> SingleDailyAsync()
    {
        await using AnalyticsDbContext context = _postgres.CreateContext();
        return await context.SystemDaily.SingleAsync();
    }

    private static BookedTransaction Booking(TransactionKind kind, decimal amount)
    {
        return Booking(kind, amount, DateTimeOffset.Parse("2026-09-17T09:30:00Z"));
    }

    private static BookedTransaction Booking(TransactionKind kind, decimal amount, DateTimeOffset bookedAt)
    {
        return BookedTransaction.Of(
            Guid.NewGuid(),
            Owner,
            kind,
            amount,
            Currency.Of("CHF"),
            bookedAt);
    }
}
