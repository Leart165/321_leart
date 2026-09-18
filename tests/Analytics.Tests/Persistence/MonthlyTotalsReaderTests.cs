using Analytics.Application.Ledger;
using Analytics.Domain.Ledger;
using Analytics.Infrastructure.Persistence;
using Xunit;

namespace Analytics.Tests.Persistence;

public sealed class MonthlyTotalsReaderTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;

    public MonthlyTotalsReaderTests(PostgresFixture postgres)
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
    public async Task Returns_only_the_totals_of_the_requested_owner_and_year()
    {
        await ApplyAsync("owner-1", 100m, "2026-09-17T09:30:00Z");
        await ApplyAsync("owner-1", 50m, "2025-09-17T09:30:00Z");
        await ApplyAsync("owner-2", 999m, "2026-09-17T09:30:00Z");

        await using AnalyticsDbContext context = _postgres.CreateContext();
        MonthlyTotalsReader reader = new MonthlyTotalsReader(context);

        IReadOnlyList<MonthlyTotal> totals = await reader.GetForOwnerAsync("owner-1", 2026, CancellationToken.None);

        MonthlyTotal total = Assert.Single(totals);
        Assert.Equal(2026, total.Year);
        Assert.Equal(9, total.Month);
        Assert.Equal(100m, total.Income);
        Assert.Equal(1, total.Transactions);
    }

    [Fact]
    public async Task Returns_an_empty_list_when_there_is_nothing_for_that_year()
    {
        await using AnalyticsDbContext context = _postgres.CreateContext();
        MonthlyTotalsReader reader = new MonthlyTotalsReader(context);

        IReadOnlyList<MonthlyTotal> totals = await reader.GetForOwnerAsync("owner-1", 2019, CancellationToken.None);

        Assert.Empty(totals);
    }

    private async Task ApplyAsync(string ownerId, decimal amount, string bookedAt)
    {
        await using AnalyticsDbContext context = _postgres.CreateContext();
        LedgerProjection projection = new LedgerProjection(new TotalsStore(context));

        BookedTransaction transaction = BookedTransaction.Of(
            Guid.NewGuid(),
            ownerId,
            TransactionKind.Deposit,
            amount,
            Currency.Of("CHF"),
            DateTimeOffset.Parse(bookedAt));

        await projection.ApplyAsync(transaction, CancellationToken.None);
    }
}
