using Analytics.Application.Ledger;
using Analytics.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Analytics.Tests.Persistence;

// Was die Bank zu einem Kunden geliefert hat: gespeichert ohne Doppelte, gelesen je Kunde und
// Zeitraum, gelöscht, sobald die Zustimmung fehlt.
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
    public async Task Every_stored_booking_is_listed_newest_first()
    {
        await RememberAsync("kunde-1", Booking("Deposit", 5400m, "2026-09-25T08:00:00Z"));
        await RememberAsync("kunde-1", Booking("Withdrawal", 42.50m, "2026-09-26T12:15:00Z"));
        await RememberAsync("kunde-1", Booking("TransferOut", 1850m, "2026-09-01T09:00:00Z"));

        IReadOnlyList<BookingEntry> log = await ReadAsync("kunde-1", "2026-09-01", "2026-09-30");

        Assert.Equal(new[] { "Withdrawal", "Deposit", "TransferOut" }, log.Select(entry => entry.Kind));
        Assert.All(log, entry => Assert.Equal("CHF", entry.Currency));
    }

    // Dieselbe Antwort der Bank zweimal, auch aus zwei Instanzen zugleich: einmal gespeichert.
    [Fact]
    public async Task A_booking_delivered_twice_is_stored_once()
    {
        BookingEntry booking = Booking("Deposit", 100m, "2026-09-17T09:30:00Z");

        await Task.WhenAll(RememberAsync("kunde-1", booking), RememberAsync("kunde-1", booking));
        await RememberAsync("kunde-1", booking);

        Assert.Single(await ReadAsync("kunde-1", "2026-09-01", "2026-09-30"));
    }

    [Fact]
    public async Task Only_the_own_bookings_inside_the_range_are_listed()
    {
        await RememberAsync("kunde-1", Booking("Deposit", 1m, "2026-08-31T23:59:59Z"));
        await RememberAsync("kunde-1", Booking("Deposit", 2m, "2026-09-01T00:00:00Z"));
        await RememberAsync("kunde-1", Booking("Deposit", 3m, "2026-09-30T23:30:00Z"));
        await RememberAsync("kunde-1", Booking("Deposit", 4m, "2026-10-01T00:00:00Z"));
        await RememberAsync("kunde-2", Booking("Deposit", 99m, "2026-09-15T10:00:00Z"));

        IReadOnlyList<BookingEntry> log = await ReadAsync("kunde-1", "2026-09-01", "2026-09-30");

        Assert.Equal(new[] { 3m, 2m }, log.Select(entry => entry.Amount));
    }

    [Fact]
    public async Task At_most_limit_bookings_are_listed()
    {
        for (int day = 1; day <= 5; day++)
        {
            await RememberAsync("kunde-1", Booking("Deposit", day, $"2026-09-{day:00}T10:00:00Z"));
        }

        await using AnalyticsDbContext context = _postgres.CreateContext();
        IReadOnlyList<BookingEntry> log = await new BookingLogReader(context).GetForOwnerAsync(
            "kunde-1", new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30), 2, CancellationToken.None);

        Assert.Equal(new[] { 5m, 4m }, log.Select(entry => entry.Amount));
    }

    [Fact]
    public async Task Forgetting_a_customer_removes_only_their_bookings()
    {
        await RememberAsync("kunde-1", Booking("Deposit", 1m, "2026-09-10T10:00:00Z"));
        await RememberAsync("kunde-2", Booking("Deposit", 2m, "2026-09-10T10:00:00Z"));

        await using (AnalyticsDbContext context = _postgres.CreateContext())
        {
            BookingStore store = new BookingStore(context, NullLogger<BookingStore>.Instance);
            Assert.True(await store.TryForgetAsync("kunde-1", CancellationToken.None));
            Assert.False(await store.KnowsAsync("kunde-1", CancellationToken.None));
            Assert.True(await store.KnowsAsync("kunde-2", CancellationToken.None));
        }

        await using AnalyticsDbContext check = _postgres.CreateContext();
        Assert.Equal(1, await check.OwnerBookings.CountAsync());
    }

    private async Task RememberAsync(string owner, BookingEntry booking)
    {
        await using AnalyticsDbContext context = _postgres.CreateContext();
        Assert.True(await new BookingStore(context, NullLogger<BookingStore>.Instance)
            .TryRememberAsync(owner, new[] { booking }, CancellationToken.None));
    }

    private async Task<IReadOnlyList<BookingEntry>> ReadAsync(string owner, string from, string to)
    {
        await using AnalyticsDbContext context = _postgres.CreateContext();
        return await new BookingLogReader(context).GetForOwnerAsync(
            owner, DateOnly.Parse(from), DateOnly.Parse(to), 500, CancellationToken.None);
    }

    private static BookingEntry Booking(string kind, decimal amount, string bookedAt)
    {
        return new BookingEntry(Guid.NewGuid(), kind, amount, "CHF", DateTimeOffset.Parse(bookedAt));
    }
}
