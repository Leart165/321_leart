using Analytics.Application.Abstractions;
using Analytics.Application.Ledger;
using Analytics.Infrastructure.Messaging;
using Analytics.Infrastructure.Persistence;
using Analytics.Tests.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using System.Text;
using System.Text.Json;
using Xunit;

namespace Analytics.Tests.Messaging;

// Was der Broker dem Benutzer analytics nach dem Contract-Schritt erlaubt und was nicht: eigene
// Queues unter analytics.*, von bank.events nur noch das Partner-Ereignis.
public sealed class BrokerTopologyTests : IClassFixture<BrokerFixture>, IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private const string Owner = "retire-test";

    private readonly BrokerFixture _broker;
    private readonly PostgresFixture _postgres;

    public BrokerTopologyTests(BrokerFixture broker, PostgresFixture postgres)
    {
        _broker = broker;
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
    public async Task The_partner_user_can_declare_the_partner_queue_and_receives_partner_events()
    {
        await using IConnection partner = await _broker.ConnectAsPartnerAsync();
        await using (IChannel channel = await partner.CreateChannelAsync())
        {
            await MessagingTopology.DeclarePartnerQueueAsync(channel, CancellationToken.None);
        }

        await PublishAsAdminAsync(MessagingTopology.PartnerRoutingKey, "{}", "PartnerTransactionCompleted");

        uint? waiting = await WaitForCountAsync(partner, MessagingTopology.PartnerQueue, expected: 1);
        Assert.Equal(1u, waiting);
    }

    // Deshalb nur passiv: bank.events gehört der Bank.
    [Fact]
    public async Task The_partner_user_may_not_declare_bank_events_actively()
    {
        await using IConnection partner = await _broker.ConnectAsPartnerAsync();
        await using IChannel channel = await partner.CreateChannelAsync();

        OperationInterruptedException refused = await Assert.ThrowsAsync<OperationInterruptedException>(() =>
            channel.ExchangeDeclareAsync(MessagingTopology.Exchange, ExchangeType.Topic, durable: true, autoDelete: false));

        Assert.Equal<int?>(Constants.AccessRefused, refused.ShutdownReason?.ReplyCode);
    }

    // Nach dem Contract-Schritt liest der Benutzer nur noch das Partner-Ereignis. Das interne
    // transaction.completed mit Buchungstext und Konto kann er nicht mehr abonnieren.
    [Fact]
    public async Task The_partner_user_may_not_subscribe_to_the_internal_event()
    {
        await using IConnection partner = await _broker.ConnectAsPartnerAsync();
        await using IChannel channel = await partner.CreateChannelAsync();
        await channel.QueueDeclareAsync("analytics.internal", durable: false, exclusive: true, autoDelete: true);

        OperationInterruptedException refused = await Assert.ThrowsAsync<OperationInterruptedException>(() =>
            channel.QueueBindAsync("analytics.internal", MessagingTopology.Exchange, "transaction.completed"));

        Assert.Equal<int?>(Constants.AccessRefused, refused.ShutdownReason?.ReplyCode);
    }

    // Deshalb ein eigener Dead-Letter-Exchange: auf bank.dlx darf der Benutzer nicht schreiben.
    [Fact]
    public async Task The_partner_user_may_not_dead_letter_into_bank_dlx()
    {
        await using IConnection partner = await _broker.ConnectAsPartnerAsync();
        await using IChannel channel = await partner.CreateChannelAsync();

        OperationInterruptedException refused = await Assert.ThrowsAsync<OperationInterruptedException>(() =>
            channel.QueueDeclareAsync("analytics.refused", durable: true, exclusive: false, autoDelete: false,
                arguments: new Dictionary<string, object?> { ["x-dead-letter-exchange"] = "bank.dlx" }));

        Assert.Equal<int?>(Constants.AccessRefused, refused.ShutdownReason?.ReplyCode);
    }

    // Scheitert der Start immer wieder, darf kein Kanal liegen bleiben: der Broker erlaubt dem
    // Benutzer analytics höchstens 50. Ist bank.events zurück, läuft der Konsument von selbst an.
    [Fact]
    public async Task Failing_starts_leave_no_channels_behind_and_the_consumer_recovers()
    {
        await using (IConnection admin = await _broker.ConnectAsAdminAsync())
        await using (IChannel channel = await admin.CreateChannelAsync())
        {
            await channel.ExchangeDeleteAsync(MessagingTopology.Exchange);
        }

        await using ServiceProvider services = Services();
        await using RabbitMqConnection connection = new RabbitMqConnection(Options.Create(PartnerOptions()));
        PartnerTransactionsConsumer consumer = new PartnerTransactionsConsumer(
            services.GetRequiredService<IServiceScopeFactory>(),
            connection,
            Options.Create(PartnerOptions()),
            NullLogger<PartnerTransactionsConsumer>.Instance);

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

            int channels = await OpenChannelsAsync();
            Assert.True(channels <= 1, $"Nach wiederholten Fehlstarts sind {channels} Kanäle offen.");
            Assert.Equal(1, connections);

            await RestoreBankEventsAsync();
            restored = true;

            int consumers = 0;
            for (int attempt = 0; attempt < 50 && consumers == 0; attempt++)
            {
                await Task.Delay(200);
                consumers = await ConsumersAsync(MessagingTopology.PartnerQueue);
            }

            Assert.Equal(1, consumers);
        }
        finally
        {
            await consumer.StopAsync(CancellationToken.None);

            // Die anderen Tests der Klasse teilen den vhost und brauchen bank.events.
            if (!restored)
            {
                await RestoreBankEventsAsync();
            }
        }
    }

    private async Task RestoreBankEventsAsync()
    {
        await using (IConnection admin = await _broker.ConnectAsAdminAsync())
        await using (IChannel channel = await admin.CreateChannelAsync())
        {
            await channel.ExchangeDeclareAsync(MessagingTopology.Exchange, ExchangeType.Topic, durable: true, autoDelete: false);
        }

        await _broker.GrantTopicPermissionAsync();
    }

    private async Task<int> OpenChannelsAsync()
    {
        JsonElement channels = await _broker.GetManagementAsync("api/vhosts/{vhost}/channels");
        return channels.GetArrayLength();
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

    private ServiceProvider Services()
    {
        ServiceCollection services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<AnalyticsDbContext>(options => options.UseNpgsql(_postgres.ConnectionString));
        services.AddScoped<ITotalsStore, TotalsStore>();
        services.AddScoped<LedgerProjection>();
        services.AddSingleton(AsyncApiSchemas.FromEmbeddedContracts());
        services.AddScoped<TransactionCompletedHandler>();
        return services.BuildServiceProvider();
    }

    private async Task PublishAsAdminAsync(string routingKey, string payload, string type)
    {
        await using IConnection admin = await _broker.ConnectAsAdminAsync();
        await using IChannel channel = await admin.CreateChannelAsync();
        BasicProperties properties = new BasicProperties
        {
            MessageId = Guid.NewGuid().ToString(),
            CorrelationId = Guid.NewGuid().ToString(),
            Type = type,
            Persistent = true
        };
        await channel.BasicPublishAsync(MessagingTopology.Exchange, routingKey, mandatory: false, properties, Encoding.UTF8.GetBytes(payload));
    }

    private static async Task<uint?> WaitForCountAsync(IConnection connection, string queue, uint expected)
    {
        uint? waiting = null;
        for (int attempt = 0; attempt < 20; attempt++)
        {
            waiting = await MessagingTopology.CountIfExistsAsync(connection, queue, CancellationToken.None);
            if (waiting == expected)
            {
                break;
            }

            await Task.Delay(100);
        }

        return waiting;
    }
}
