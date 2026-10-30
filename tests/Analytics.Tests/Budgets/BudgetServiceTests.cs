using Analytics.Application.Abstractions;
using Analytics.Application.Bank;
using Analytics.Application.Budgets;
using Analytics.Application.Ledger;
using Analytics.Domain.Budgets;
using Analytics.Infrastructure.Messaging;
using Analytics.Infrastructure.Outbox;
using Analytics.Infrastructure.Persistence;
using Analytics.Tests.Persistence;
using Analytics.Tests.Reports;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using Xunit;

namespace Analytics.Tests.Budgets;

// Budget gegen eine echte Datenbank: festlegen, im Hintergrund prüfen, jede Stufe genau einmal
// melden, auch wenn zwei Instanzen gleichzeitig prüfen.
public sealed class BudgetServiceTests : IClassFixture<PostgresFixture>, IAsyncLifetime, IDisposable
{
    private const string Owner = "kunde-1";

    private static readonly DateTimeOffset Now = new DateTimeOffset(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);

    private readonly PostgresFixture _postgres;
    private readonly ServiceProvider _services;

    public BudgetServiceTests(PostgresFixture postgres)
    {
        _postgres = postgres;
        _services = ReportServiceTests.Services(postgres.ConnectionString, new FixedClock(Now));
    }

    public Task InitializeAsync()
    {
        return _postgres.ResetAsync();
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _services.Dispose();
    }

    [Fact]
    public async Task Setting_a_budget_stores_it_and_asks_for_a_check_of_the_current_month()
    {
        await StoreAsync(Owner, "Withdrawal", 300m, new DateTimeOffset(2026, 10, 3, 10, 0, 0, TimeSpan.Zero));

        BudgetStatus status = await WithServiceAsync(service =>
            service.SetAsync(Owner, true, "CHF", 1000m, "corr-b", CancellationToken.None));

        Assert.Equal(1000m, status.Budget.Limit);
        Assert.Equal((2026, 10), (status.Year, status.Month));
        Assert.Equal(300m, status.Spent);
        Assert.Equal(BudgetLevel.Ok, status.Level);
        Assert.Empty(status.Alerts);

        await using AnalyticsDbContext context = _postgres.CreateContext();
        OutboxMessage message = await context.Outbox.SingleAsync();
        Assert.Equal(BudgetTopology.CheckRequestedRoutingKey, message.RoutingKey);
        Assert.Equal(BudgetTopology.CheckRequestedType, message.Type);
        Assert.Equal("corr-b", message.CorrelationId);
        using JsonDocument payload = JsonDocument.Parse(message.Payload);
        Assert.Equal(Owner, payload.RootElement.GetProperty("ownerId").GetString());
        Assert.Equal(10, payload.RootElement.GetProperty("month").GetInt32());
    }

    [Fact]
    public async Task Each_level_is_reported_once_per_month()
    {
        await SetAsync(1000m);
        await StoreAsync(Owner, "Withdrawal", 850m, new DateTimeOffset(2026, 10, 3, 10, 0, 0, TimeSpan.Zero));

        Assert.Equal(BudgetCheck.Raised, await CheckAsync());
        Assert.Equal(BudgetCheck.Unchanged, await CheckAsync());

        await StoreAsync(Owner, "TransferOut", 200m, new DateTimeOffset(2026, 10, 14, 10, 0, 0, TimeSpan.Zero));
        Assert.Equal(BudgetCheck.Raised, await CheckAsync());
        Assert.Equal(BudgetCheck.Unchanged, await CheckAsync());

        BudgetStatus status = await WithServiceAsync(service => service.GetStatusAsync(Owner, true, CancellationToken.None));
        Assert.Equal(BudgetLevel.Exceeded, status.Level);
        Assert.Equal(1050m, status.Spent);
        Assert.Equal(new[] { BudgetLevel.Warning, BudgetLevel.Exceeded }, status.Alerts.Select(alert => alert.Level).Order());
    }

    [Fact]
    public async Task Only_expenses_of_the_month_in_the_budget_currency_count()
    {
        await SetAsync(100m);
        await StoreAsync(Owner, "Deposit", 5000m, new DateTimeOffset(2026, 10, 2, 10, 0, 0, TimeSpan.Zero));
        await StoreAsync(Owner, "TransferIn", 5000m, new DateTimeOffset(2026, 10, 2, 11, 0, 0, TimeSpan.Zero));
        await StoreAsync(Owner, "Withdrawal", 5000m, new DateTimeOffset(2026, 9, 30, 23, 0, 0, TimeSpan.Zero));
        await StoreAsync(Owner, "Withdrawal", 5000m, new DateTimeOffset(2026, 10, 2, 12, 0, 0, TimeSpan.Zero), "EUR");
        await StoreAsync("jemand-anders", "Withdrawal", 5000m, new DateTimeOffset(2026, 10, 2, 12, 0, 0, TimeSpan.Zero));
        await StoreAsync(Owner, "Withdrawal", 10m, new DateTimeOffset(2026, 10, 5, 12, 0, 0, TimeSpan.Zero));

        Assert.Equal(BudgetCheck.Unchanged, await CheckAsync());

        BudgetStatus status = await WithServiceAsync(service => service.GetStatusAsync(Owner, true, CancellationToken.None));
        Assert.Equal(10m, status.Spent);
        Assert.Equal(0.1m, status.Ratio);
    }

    [Fact]
    public async Task When_two_instances_check_at_the_same_time_the_alert_exists_once()
    {
        await SetAsync(100m);
        await StoreAsync(Owner, "Withdrawal", 90m, new DateTimeOffset(2026, 10, 3, 10, 0, 0, TimeSpan.Zero));

        using IServiceScope first = _services.CreateScope();
        using IServiceScope second = _services.CreateScope();
        BudgetCheck[] results = await Task.WhenAll(
            first.ServiceProvider.GetRequiredService<BudgetService>().CheckAsync(Owner, 2026, 10, CancellationToken.None),
            second.ServiceProvider.GetRequiredService<BudgetService>().CheckAsync(Owner, 2026, 10, CancellationToken.None));

        Assert.Contains(BudgetCheck.Raised, results);
        await using AnalyticsDbContext context = _postgres.CreateContext();
        Assert.Equal(1, await context.BudgetAlerts.CountAsync());
    }

    [Fact]
    public async Task Without_budget_there_is_nothing_to_check_or_show()
    {
        Assert.Equal(BudgetCheck.NoBudget, await CheckAsync());
        await Assert.ThrowsAsync<BudgetNotFoundException>(() =>
            WithServiceAsync(service => service.GetStatusAsync(Owner, true, CancellationToken.None)));
        await Assert.ThrowsAsync<BudgetNotFoundException>(() =>
            WithServiceAsync(async service =>
            {
                await service.RemoveAsync(Owner, true, CancellationToken.None);
                return 0;
            }));
    }

    [Fact]
    public async Task Without_consent_there_is_no_budget()
    {
        await Assert.ThrowsAsync<BankAccessDeniedException>(() =>
            WithServiceAsync(service => service.SetAsync(Owner, false, "CHF", 100m, "corr", CancellationToken.None)));
        await SetAsync(100m);
        await Assert.ThrowsAsync<BankAccessDeniedException>(() =>
            WithServiceAsync(service => service.GetStatusAsync(Owner, false, CancellationToken.None)));
    }

    [Fact]
    public async Task New_bookings_ask_for_a_check_only_when_there_is_a_budget()
    {
        await WithServiceAsync(async service =>
        {
            await service.BookingsStoredAsync(Owner, 3, "corr-ohne-budget", CancellationToken.None);
            return 0;
        });
        await SetAsync(100m);
        await WithServiceAsync(async service =>
        {
            await service.BookingsStoredAsync(Owner, 0, "corr-nichts-neu", CancellationToken.None);
            await service.BookingsStoredAsync(Owner, 2, "corr-neu", CancellationToken.None);
            return 0;
        });

        await using AnalyticsDbContext context = _postgres.CreateContext();
        List<string> correlations = await context.Outbox.Select(message => message.CorrelationId).OrderBy(id => id).ToListAsync();
        Assert.Equal(new[] { "corr", "corr-neu" }, correlations);
    }

    [Fact]
    public async Task Removing_the_budget_stops_the_checks()
    {
        await SetAsync(100m);
        await StoreAsync(Owner, "Withdrawal", 100m, new DateTimeOffset(2026, 10, 3, 10, 0, 0, TimeSpan.Zero));
        Assert.Equal(BudgetCheck.Raised, await CheckAsync());

        await WithServiceAsync(async service =>
        {
            await service.RemoveAsync(Owner, true, CancellationToken.None);
            return 0;
        });

        await using AnalyticsDbContext context = _postgres.CreateContext();
        Assert.Equal(0, await context.Budgets.CountAsync());
        Assert.Equal(BudgetCheck.NoBudget, await CheckAsync());
    }

    private Task<BudgetStatus> SetAsync(decimal limit)
    {
        return WithServiceAsync(service => service.SetAsync(Owner, true, "CHF", limit, "corr", CancellationToken.None));
    }

    private Task<BudgetCheck> CheckAsync()
    {
        return WithServiceAsync(service => service.CheckAsync(Owner, 2026, 10, CancellationToken.None));
    }

    private async Task<T> WithServiceAsync<T>(Func<BudgetService, Task<T>> action)
    {
        using IServiceScope scope = _services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<BudgetService>());
    }

    private async Task StoreAsync(string owner, string kind, decimal amount, DateTimeOffset bookedAt, string currency = "CHF")
    {
        using IServiceScope scope = _services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IBookingStore>().TryRememberAsync(
            owner, new[] { new BookingEntry(Guid.NewGuid(), kind, amount, currency, bookedAt) }, CancellationToken.None);
    }
}
