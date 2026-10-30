using Analytics.Application.Abstractions;
using Analytics.Application.Budgets;
using Analytics.Application.Ledger;
using Analytics.Application.Reports;
using Analytics.Domain.Budgets;
using Analytics.Domain.Reports;
using Analytics.Infrastructure.Messaging;
using Analytics.Infrastructure.Outbox;
using Analytics.Infrastructure.Persistence;
using Analytics.Tests.Persistence;
using Analytics.Tests.Reports;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using System.Text;
using Xunit;

namespace Analytics.Tests.Messaging;

// Die zwei weiteren Queues am eigenen Broker: der Jahresauszug über analytics.annual-reports und
// die Budgetprüfung über analytics.budgets, jeweils mit eigener DLQ.
public sealed class AnnualAndBudgetFlowTests : IClassFixture<BrokerFixture>, IClassFixture<PostgresFixture>, IAsyncLifetime, IDisposable
{
    private const string Owner = "kunde-1";

    private readonly BrokerFixture _broker;
    private readonly PostgresFixture _postgres;
    private readonly ServiceProvider _services;

    public AnnualAndBudgetFlowTests(BrokerFixture broker, PostgresFixture postgres)
    {
        _broker = broker;
        _postgres = postgres;
        _services = ReportServiceTests.Services(postgres.ConnectionString);
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
    public async Task An_annual_statement_travels_through_its_own_queue_and_ends_as_a_ready_pdf()
    {
        await using RabbitMqConnection connection = new RabbitMqConnection(Options.Create(ServiceOptions()));
        AnnualReportRequestedConsumer consumer = new AnnualReportRequestedConsumer(
            _services.GetRequiredService<IServiceScopeFactory>(), connection, Options.Create(ServiceOptions()),
            NullLogger<AnnualReportRequestedConsumer>.Instance);

        await consumer.StartAsync(CancellationToken.None);
        await DeclareAsync(ReportTopology.DeclareAnnualReportsQueueAsync);
        try
        {
            MonthlyReport report;
            using (IServiceScope scope = _services.CreateScope())
            {
                report = await scope.ServiceProvider.GetRequiredService<ReportService>()
                    .RequestAnnualAsync(Owner, true, 2025, "flow-jahr", CancellationToken.None);
            }

            Assert.Equal(1, await Dispatcher(connection).DispatchBatchAsync(CancellationToken.None));

            ReportStatus status = ReportStatus.Requested;
            for (int attempt = 0; attempt < 50 && status == ReportStatus.Requested; attempt++)
            {
                await Task.Delay(200);
                await using AnalyticsDbContext context = _postgres.CreateContext();
                status = (await context.MonthlyReports.SingleAsync(entry => entry.Id == report.Id)).Status;
            }

            Assert.Equal(ReportStatus.Ready, status);
        }
        finally
        {
            await consumer.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task A_budget_check_travels_through_analytics_budgets_and_raises_the_alert()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        using (IServiceScope scope = _services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IBookingStore>().TryRememberAsync(
                Owner, new[] { new BookingEntry(Guid.NewGuid(), "Withdrawal", 120m, "CHF", now.AddMinutes(-1)) }, CancellationToken.None);
        }

        await using RabbitMqConnection connection = new RabbitMqConnection(Options.Create(ServiceOptions()));
        BudgetCheckConsumer consumer = new BudgetCheckConsumer(
            _services.GetRequiredService<IServiceScopeFactory>(), connection, Options.Create(ServiceOptions()),
            NullLogger<BudgetCheckConsumer>.Instance);

        await consumer.StartAsync(CancellationToken.None);
        await DeclareAsync(BudgetTopology.DeclareBudgetsQueueAsync);
        try
        {
            using (IServiceScope scope = _services.CreateScope())
            {
                await scope.ServiceProvider.GetRequiredService<BudgetService>()
                    .SetAsync(Owner, true, "CHF", 100m, "flow-budget", CancellationToken.None);
            }

            Assert.Equal(1, await Dispatcher(connection).DispatchBatchAsync(CancellationToken.None));

            int alerts = 0;
            for (int attempt = 0; attempt < 50 && alerts < 2; attempt++)
            {
                await Task.Delay(200);
                await using AnalyticsDbContext context = _postgres.CreateContext();
                alerts = await context.BudgetAlerts.CountAsync();
            }

            await using AnalyticsDbContext check = _postgres.CreateContext();
            Assert.Equal(
                new[] { BudgetLevel.Warning, BudgetLevel.Exceeded },
                (await check.BudgetAlerts.Select(alert => alert.Level).ToListAsync()).Order());
        }
        finally
        {
            await consumer.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task A_budget_check_that_breaks_the_contract_goes_to_its_dead_letter_queue()
    {
        await using RabbitMqConnection connection = new RabbitMqConnection(Options.Create(ServiceOptions()));
        BudgetCheckConsumer consumer = new BudgetCheckConsumer(
            _services.GetRequiredService<IServiceScopeFactory>(), connection, Options.Create(ServiceOptions()),
            NullLogger<BudgetCheckConsumer>.Instance);

        await consumer.StartAsync(CancellationToken.None);
        try
        {
            await using IConnection service = await _broker.ConnectAsServiceAsync();
            await using (IChannel channel = await service.CreateChannelAsync())
            {
                await BudgetTopology.DeclareBudgetsQueueAsync(channel, CancellationToken.None);
                BasicProperties properties = new BasicProperties { MessageId = Guid.NewGuid().ToString(), Type = BudgetTopology.CheckRequestedType };
                await channel.BasicPublishAsync(ReportTopology.Exchange, BudgetTopology.CheckRequestedRoutingKey, mandatory: false,
                    properties, Encoding.UTF8.GetBytes("{\"ownerId\":\"kunde-1\",\"year\":2026,\"month\":13}"));
            }

            uint waiting = 0;
            for (int attempt = 0; attempt < 50 && waiting == 0; attempt++)
            {
                await Task.Delay(200);
                waiting = await MessagingTopology.CountIfExistsAsync(service, BudgetTopology.BudgetsDeadLetterQueue, CancellationToken.None) ?? 0;
            }

            Assert.Equal(1u, waiting);
        }
        finally
        {
            await consumer.StopAsync(CancellationToken.None);
        }
    }

    // Der Konsument deklariert seine Queue im Hintergrund; ohne sie ginge eine sofort veröffentlichte
    // Nachricht verloren. Im Betrieb steht die Queue längst.
    private async Task DeclareAsync(Func<IChannel, CancellationToken, Task> declare)
    {
        await using IConnection service = await _broker.ConnectAsServiceAsync();
        await using IChannel channel = await service.CreateChannelAsync();
        await declare(channel, CancellationToken.None);
    }

    private OutboxDispatcher Dispatcher(RabbitMqConnection connection)
    {
        return new OutboxDispatcher(
            _services.GetRequiredService<IServiceScopeFactory>(), connection, Options.Create(ServiceOptions()),
            NullLogger<OutboxDispatcher>.Instance);
    }

    private MessagingOptions ServiceOptions()
    {
        return new MessagingOptions
        {
            Host = _broker.BrokerHost,
            User = _broker.ServiceUser,
            Password = _broker.ServicePassword,
            VirtualHost = _broker.VirtualHost,
            RetryDelay = TimeSpan.FromMilliseconds(200)
        };
    }
}
