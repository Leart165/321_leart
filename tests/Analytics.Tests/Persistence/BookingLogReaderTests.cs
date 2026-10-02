using Analytics.Application.Ledger;
using Analytics.Domain.Ledger;
using Analytics.Infrastructure.Persistence;
using Xunit;

namespace Analytics.Tests.Persistence;

// Das Buchungsprotokoll entsteht beim Zählen, in derselben Transaktion wie die Summen.
public sealed class BookingLogReaderTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;

    public BookingLogReaderTests(PostgresFixture postgres)
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
    public async Task Every_counted_booking_is_listed_newest_first()
    {
        await ApplyAsync(Booking("kunde-1", TransactionKind.Deposit, 5400m, "2026-09-25T08:00:00Z"));
        await ApplyAsync(Booking("kunde-1", TransactionKind.Withdrawal, 42.50m, "2026-09-26T12:15:00Z"));
        await ApplyAsync(Booking("kunde-1", TransactionKind.TransferOut, 1850m, "2026-09-01T09:00:00Z"));

        IReadOnlyList<BookingEntry> log = await ReadAsync("kunde-1", "2026-09-01", "2026-09-30");

        Assert.Collection(
            log,
            first => Assert.Equal((TransactionKind.Withdrawal.ToString(), 42.50m), (first.Kind, first.Amount)),
            second => Assert.Equal((TransactionKind.Deposit.ToString(), 5400m), (second.Kind, second.Amount)),
            third => Assert.Equal((TransactionKind.TransferOut.ToString(), 1850m), (third.Kind, third.Amount)));
        Assert.All(log, entry => Assert.Equal("CHF", entry.Currency));
    }

    [Fact]
    public async Task A_booking_delivered_twice_is_listed_once()
    {
        BookedTransaction booking = Booking("kunde-1", TransactionKind.Deposit, 100m, "2026-09-17T09:30:00Z");

        await ApplyAsync(booking);
        await ApplyAsync(booking);

        Assert.Single(await ReadAsync("kunde-1", "2026-09-01", "2026-09-30"));
    }

    [Fact]
    public async Task Only_the_own_bookings_inside_the_range_are_listed()
    {
        await ApplyAsync(Booking("kunde-1", TransactionKind.Deposit, 1m, "2026-08-31T23:59:59Z"));
        await ApplyAsync(Booking("kunde-1", TransactionKind.Deposit, 2m, "2026-09-01T00:00:00Z"));
        await ApplyAsync(Booking("kunde-1", TransactionKind.Deposit, 3m, "2026-09-30T23:30:00Z"));
        await ApplyAsync(Booking("kunde-1", TransactionKind.Deposit, 4m, "2026-10-01T00:00:00Z"));
        await ApplyAsync(Booking("kunde-2", TransactionKind.Deposit, 99m, "2026-09-15T10:00:00Z"));

        IReadOnlyList<BookingEntry> log = await ReadAsync("kunde-1", "2026-09-01", "2026-09-30");

        Assert.Equal(new[] { 3m, 2m }, log.Select(entry => entry.Amount));
    }

    [Fact]
    public async Task At_most_limit_bookings_are_listed()
    {
        for (int day = 1; day <= 5; day++)
        {
            await ApplyAsync(Booking("kunde-1", TransactionKind.Deposit, day, $"2026-09-{day:00}T10:00:00Z"));
        }

        await using AnalyticsDbContext context = _postgres.CreateContext();
        IReadOnlyList<BookingEntry> log = await new BookingLogReader(context).GetForOwnerAsync(
            "kunde-1", new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30), 2, CancellationToken.None);

        Assert.Equal(new[] { 5m, 4m }, log.Select(entry => entry.Amount));
    }

    private async Task ApplyAsync(BookedTransaction transaction)
    {
        await using AnalyticsDbContext context = _postgres.CreateContext();
        await new LedgerProjection(new TotalsStore(context)).ApplyAsync(transaction, CancellationToken.None);
    }

    private async Task<IReadOnlyList<BookingEntry>> ReadAsync(string owner, string from, string to)
    {
        await using AnalyticsDbContext context = _postgres.CreateContext();
        return await new BookingLogReader(context).GetForOwnerAsync(
            owner, DateOnly.Parse(from), DateOnly.Parse(to), 500, CancellationToken.None);
    }

    private static BookedTransaction Booking(string owner, TransactionKind kind, decimal amount, string bookedAt)
    {
        return BookedTransaction.Of(Guid.NewGuid(), owner, kind, amount, Currency.Of("CHF"), DateTimeOffset.Parse(bookedAt));
    }
}
