using Analytics.Application.Reports;
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

// Der ganze Weg eines Antrags über den eigenen Exchange, mit genau den Rechten des Benutzers
// analytics: Outbox, analytics.events, analytics.reports, PDF.
public sealed class ReportFlowTests : IClassFixture<BrokerFixture>, IClassFixture<PostgresFixture>, IAsyncLifetime, IDisposable
{
    private readonly BrokerFixture _broker;
    private readonly PostgresFixture _postgres;
    private readonly ServiceProvider _services;

    public ReportFlowTests(BrokerFixture broker, PostgresFixture postgres)
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
    public async Task A_request_travels_through_analytics_events_and_ends_as_a_ready_pdf()
    {
        await using RabbitMqConnection connection = new RabbitMqConnection(Options.Create(PartnerOptions()));
        ReportRequestedConsumer consumer = new ReportRequestedConsumer(
            _services.GetRequiredService<IServiceScopeFactory>(), connection, Options.Create(PartnerOptions()),
            NullLogger<ReportRequestedConsumer>.Instance);
        OutboxDispatcher dispatcher = new OutboxDispatcher(
            _services.GetRequiredService<IServiceScopeFactory>(), connection, Options.Create(PartnerOptions()),
            NullLogger<OutboxDispatcher>.Instance);

        await consumer.StartAsync(CancellationToken.None);
        try
        {
            MonthlyReport report;
            using (IServiceScope scope = _services.CreateScope())
            {
                report = await scope.ServiceProvider.GetRequiredService<ReportService>()
                    .RequestAsync("kunde-1", 2026, 9, "flow-1", CancellationToken.None);
            }

            Assert.Equal(1, await dispatcher.DispatchBatchAsync(CancellationToken.None));
            Assert.Equal(0, await dispatcher.DispatchBatchAsync(CancellationToken.None));

            ReportStatus status = ReportStatus.Requested;
            for (int attempt = 0; attempt < 50 && status == ReportStatus.Requested; attempt++)
            {
                await Task.Delay(200);
                await using AnalyticsDbContext context = _postgres.CreateContext();
                status = (await context.MonthlyReports.SingleAsync(entry => entry.Id == report.Id)).Status;
            }

            Assert.Equal(ReportStatus.Ready, status);
            await using AnalyticsDbContext check = _postgres.CreateContext();
            Assert.NotNull((await check.Outbox.SingleAsync()).PublishedAt);
            Assert.Equal(1, await check.ReportDocuments.CountAsync());
        }
        finally
        {
            await consumer.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task A_request_that_breaks_the_contract_goes_to_the_dead_letter_queue()
    {
        await using RabbitMqConnection connection = new RabbitMqConnection(Options.Create(PartnerOptions()));
        ReportRequestedConsumer consumer = new ReportRequestedConsumer(
            _services.GetRequiredService<IServiceScopeFactory>(), connection, Options.Create(PartnerOptions()),
            NullLogger<ReportRequestedConsumer>.Instance);

        await consumer.StartAsync(CancellationToken.None);
        try
        {
            await using IConnection partner = await _broker.ConnectAsPartnerAsync();
            await using (IChannel channel = await partner.CreateChannelAsync())
            {
                await ReportTopology.DeclareReportsQueueAsync(channel, CancellationToken.None);
                BasicProperties properties = new BasicProperties { MessageId = Guid.NewGuid().ToString(), Type = ReportTopology.ReportRequestedType };
                await channel.BasicPublishAsync(ReportTopology.Exchange, ReportTopology.ReportRequestedRoutingKey, mandatory: false,
                    properties, Encoding.UTF8.GetBytes("{\"reportId\":\"kein-guid\"}"));
            }

            uint waiting = 0;
            for (int attempt = 0; attempt < 50 && waiting == 0; attempt++)
            {
                await Task.Delay(200);
                waiting = await MessagingTopology.CountIfExistsAsync(partner, ReportTopology.ReportsDeadLetterQueue, CancellationToken.None) ?? 0;
            }

            Assert.Equal(1u, waiting);
        }
        finally
        {
            await consumer.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task The_partner_user_may_create_its_own_exchange_but_not_publish_to_bank_events()
    {
        await using IConnection partner = await _broker.ConnectAsPartnerAsync();
        await using (IChannel channel = await partner.CreateChannelAsync())
        {
            await ReportTopology.DeclareReportsQueueAsync(channel, CancellationToken.None);
        }

        await using IChannel forbidden = await partner.CreateChannelAsync();
        await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            await forbidden.BasicPublishAsync(MessagingTopology.Exchange, ReportTopology.ReportRequestedRoutingKey, Encoding.UTF8.GetBytes("{}"));
            await forbidden.QueueDeclarePassiveAsync(ReportTopology.ReportsQueue);
        });
    }

    private MessagingOptions PartnerOptions()
    {
        return new MessagingOptions
        {
            Host = _broker.BrokerHost,
            User = _broker.PartnerUser,
            Password = _broker.PartnerPassword,
            VirtualHost = _broker.VirtualHost,
            RetryDelay = TimeSpan.FromMilliseconds(200)
        };
    }
}
