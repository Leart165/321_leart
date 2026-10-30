using Analytics.Application.Ledger;
using Analytics.Infrastructure.Persistence;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Analytics.Tests.Persistence;

// Tagessummen über die Buchungen, die Kunden mit ihrer Zustimmung bei uns gespeichert haben.
public sealed class DailyTotalsReaderTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;

    public DailyTotalsReaderTests(PostgresFixture postgres)
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
    public async Task Returns_only_the_utc_days_inside_the_range_both_ends_included()
    {
        await StoreAsync("kunde-1", "Deposit", 1m, "2026-08-31T23:59:59Z");
        await StoreAsync("kunde-1", "Deposit", 100m, "2026-09-01T00:30:00Z");
        await StoreAsync("kunde-2", "Deposit", 200m, "2026-09-30T23:30:00Z");
        await StoreAsync("kunde-1", "Deposit", 999m, "2026-10-01T00:00:00Z");

        IReadOnlyList<DailyTotal> totals = await ReadAsync(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30));

        Assert.Equal(new[] { new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30) }, totals.Select(total => total.Day));
    }

    // Wie bei der Bank: Volumen aus Ein-, Auszahlungen und ausgehenden Überweisungen; eine
    // Überweisung zählt nur auf der ausgehenden Seite, sonst wäre sie doppelt.
    [Fact]
    public async Task A_day_sums_all_customers_per_currency()
    {
        await StoreAsync("kunde-1", "Deposit", 100m, "2026-09-17T08:00:00Z");
        await StoreAsync("kunde-2", "Withdrawal", 30m, "2026-09-17T09:00:00Z");
        await StoreAsync("kunde-1", "TransferOut", 20m, "2026-09-17T10:00:00Z");
        await StoreAsync("kunde-2", "TransferIn", 20m, "2026-09-17T10:00:00Z");
        await StoreAsync("kunde-1", "Deposit", 5m, "2026-09-17T11:00:00Z", "EUR");

        IReadOnlyList<DailyTotal> totals = await ReadAsync(new DateOnly(2026, 9, 17), new DateOnly(2026, 9, 17));

        Assert.Collection(
            totals,
            chf => Assert.Equal(new DailyTotal(new DateOnly(2026, 9, 17), "CHF", 150m, 1, 1, 1), chf),
            eur => Assert.Equal(new DailyTotal(new DateOnly(2026, 9, 17), "EUR", 5m, 1, 0, 0), eur));
    }

    private async Task<IReadOnlyList<DailyTotal>> ReadAsync(DateOnly from, DateOnly to)
    {
        await using AnalyticsDbContext context = _postgres.CreateContext();
        return await new DailyTotalsReader(context).GetForRangeAsync(from, to, CancellationToken.None);
    }

    private async Task StoreAsync(string owner, string kind, decimal amount, string bookedAt, string currency = "CHF")
    {
        await using AnalyticsDbContext context = _postgres.CreateContext();
        await new BookingStore(context, NullLogger<BookingStore>.Instance).TryRememberAsync(
            owner,
            new[] { new BookingEntry(Guid.NewGuid(), kind, amount, currency, DateTimeOffset.Parse(bookedAt)) },
            CancellationToken.None);
    }
}
