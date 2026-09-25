using Analytics.Application.Ledger;
using Analytics.Domain.Ledger;
using Analytics.Infrastructure.Persistence;
using Xunit;

namespace Analytics.Tests.Persistence;

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
    public async Task Returns_only_the_days_inside_the_requested_range()
    {
        await ApplyAsync("2026-09-05T09:00:00Z", 100m);
        await ApplyAsync("2026-09-17T09:00:00Z", 250m);
        await ApplyAsync("2026-10-01T09:00:00Z", 999m);

        await using AnalyticsDbContext context = _postgres.CreateContext();
        DailyTotalsReader reader = new DailyTotalsReader(context);

        IReadOnlyList<DailyTotal> totals = await reader.GetForRangeAsync(
            new DateOnly(2026, 9, 1),
            new DateOnly(2026, 9, 30),
            CancellationToken.None);

        Assert.Equal(2, totals.Count);
        Assert.Equal(new DateOnly(2026, 9, 5), totals[0].Day);
        Assert.Equal(new DateOnly(2026, 9, 17), totals[1].Day);
    }

    [Fact]
    public async Task The_range_is_inclusive_on_both_ends()
    {
        await ApplyAsync("2026-09-01T00:30:00Z", 100m);
        await ApplyAsync("2026-09-30T23:30:00Z", 200m);

        await using AnalyticsDbContext context = _postgres.CreateContext();
        DailyTotalsReader reader = new DailyTotalsReader(context);

        IReadOnlyList<DailyTotal> totals = await reader.GetForRangeAsync(
            new DateOnly(2026, 9, 1),
            new DateOnly(2026, 9, 30),
            CancellationToken.None);

        Assert.Equal(2, totals.Count);
    }

    private async Task ApplyAsync(string bookedAt, decimal amount)
    {
        await using AnalyticsDbContext context = _postgres.CreateContext();
        LedgerProjection projection = new LedgerProjection(new TotalsStore(context));

        BookedTransaction transaction = BookedTransaction.Of(
            Guid.NewGuid(),
            "owner-1",
            TransactionKind.Deposit,
            amount,
            Currency.Of("CHF"),
            DateTimeOffset.Parse(bookedAt));

        await projection.ApplyAsync(transaction, CancellationToken.None);
    }
}
