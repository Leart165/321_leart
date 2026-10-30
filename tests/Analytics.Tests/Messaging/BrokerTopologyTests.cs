using Analytics.Infrastructure.Messaging;
using Analytics.Tests.Persistence;
using Analytics.Tests.Reports;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using System.Text.Json;
using Xunit;

namespace Analytics.Tests.Messaging;

// Der Konsument am eigenen Broker übersteht Fehlstarts, ohne Kanäle liegen zu lassen.
public sealed class BrokerTopologyTests : IClassFixture<BrokerFixture>, IClassFixture<PostgresFixture>
{
    private readonly BrokerFixture _broker;
    private readonly PostgresFixture _postgres;

    public BrokerTopologyTests(BrokerFixture broker, PostgresFixture postgres)
    {
        _broker = broker;
        _postgres = postgres;
    }

    // Scheitert der Start immer wieder, darf kein Kanal liegen bleiben: ein Broker begrenzt die
    // Kanäle je Verbindung. Sind die Rechte zurück, läuft der Konsument von selbst an.
    [Fact]
    public async Task Failing_starts_leave_no_channels_behind_and_the_consumer_recovers()
    {
        await _broker.RevokeServiceUserAsync();

        await using ServiceProvider services = ReportServiceTests.Services(_postgres.ConnectionString);
        await using RabbitMqConnection connection = new RabbitMqConnection(Options.Create(ServiceOptions()));
        ReportRequestedConsumer consumer = new ReportRequestedConsumer(
            services.GetRequiredService<IServiceScopeFactory>(),
            connection,
            Options.Create(ServiceOptions()),
            NullLogger<ReportRequestedConsumer>.Instance);

        bool restored = false;
        await consumer.StartAsync(CancellationToken.None);
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(3));

            // Die Management-API führt ihre Liste mit Verzögerung; deshalb warten statt einmal fragen.
            int connections = 0;
            for (int attempt = 0; attempt < 50 && connections == 0; attempt++)
            {
                connections = (await _broker.GetManagementAsync("api/vhosts/{vhost}/connections")).GetArrayLength();
                if (connections == 0)
                {
                    await Task.Delay(200);
                }
            }

            int channels = (await _broker.GetManagementAsync("api/vhosts/{vhost}/channels")).GetArrayLength();
            Assert.True(channels <= 1, $"Nach wiederholten Fehlstarts sind {channels} Kanäle offen.");
            Assert.Equal(1, connections);

            await _broker.GrantServiceUserAsync();
            restored = true;

            int consumers = 0;
            for (int attempt = 0; attempt < 50 && consumers == 0; attempt++)
            {
                await Task.Delay(200);
                consumers = await ConsumersAsync(ReportTopology.ReportsQueue);
            }

            Assert.Equal(1, consumers);
        }
        finally
        {
            await consumer.StopAsync(CancellationToken.None);

            // Die anderen Tests der Klasse teilen den virtuellen Host und brauchen die Rechte.
            if (!restored)
            {
                await _broker.GrantServiceUserAsync();
            }
        }
    }

    private async Task<int> ConsumersAsync(string queue)
    {
        try
        {
            JsonElement details = await _broker.GetManagementAsync($"api/queues/{{vhost}}/{queue}");
            return details.TryGetProperty("consumers", out JsonElement consumers) ? consumers.GetInt32() : 0;
        }
        catch (HttpRequestException)
        {
            return 0;
        }
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
