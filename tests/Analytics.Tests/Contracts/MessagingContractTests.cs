using Analytics.Infrastructure.Messaging;
using System.Text.Json;
using Xunit;

namespace Analytics.Tests.Contracts;

// Die Namen am Broker müssen genau die aus den Kontrakten sein: Version 1 für die alte Queue,
// Version 2 für die Partner-Queue. Sonst bindet sich der Dienst an etwas, das die Bank nicht kennt.
public sealed class MessagingContractTests
{
    private static JsonElement Operation(string contract, string operation)
    {
        using JsonDocument document = ContractFile.Load(contract);
        return document.RootElement.GetProperty("operations").GetProperty(operation).Clone();
    }

    private static JsonElement Channel(string contract, string channel)
    {
        using JsonDocument document = ContractFile.Load(contract);
        return document.RootElement.GetProperty("channels").GetProperty(channel).Clone();
    }

    private static string ExchangeOf(JsonElement channel)
    {
        return channel.GetProperty("bindings").GetProperty("amqp").GetProperty("exchange").GetProperty("name").GetString()!;
    }

    [Fact]
    public void Version2_binds_the_partner_queue_as_the_code_does()
    {
        JsonElement queue = Operation("analytics/asyncapi.v2.yaml", "consumePartnerTransactionCompleted").GetProperty("x-queue");
        JsonElement channel = Channel("analytics/asyncapi.v2.yaml", "partnerTransactionCompleted");

        Assert.Equal(MessagingTopology.PartnerQueue, queue.GetProperty("name").GetString());
        Assert.Equal(MessagingTopology.PartnerDeadLetterQueue, queue.GetProperty("deadLetterQueue").GetString());
        Assert.Equal(MessagingTopology.DeadLetterExchange, queue.GetProperty("deadLetterExchange").GetString());
        Assert.Equal(new MessagingOptions().Prefetch.ToString(), queue.GetProperty("prefetch").ToString());
        Assert.Equal(new MessagingOptions().ClientName, queue.GetProperty("consumer").GetString());
        Assert.Equal(MessagingTopology.PartnerRoutingKey, channel.GetProperty("address").GetString());
        Assert.Equal(MessagingTopology.Exchange, ExchangeOf(channel));
    }

    [Fact]
    public void The_bank_publishes_the_partner_event_where_the_queue_listens()
    {
        JsonElement channel = Channel("partner/asyncapi.v1.yaml", "partnerTransactionCompleted");

        Assert.Equal(MessagingTopology.PartnerRoutingKey, channel.GetProperty("address").GetString());
        Assert.Equal(MessagingTopology.Exchange, ExchangeOf(channel));
    }

    // Der Broker-Benutzer analytics darf nur analytics.* anlegen und beschreiben.
    [Fact]
    public void Everything_this_service_creates_starts_with_analytics()
    {
        string[] created =
        {
            MessagingTopology.PartnerQueue,
            MessagingTopology.PartnerDeadLetterQueue,
            MessagingTopology.DeadLetterExchange
        };

        Assert.All(created, name => Assert.StartsWith("analytics.", name, StringComparison.Ordinal));
    }

    [Fact]
    public void Version1_still_describes_the_legacy_queue_that_is_drained()
    {
        JsonElement queue = Operation("analytics/asyncapi.v1.yaml", "consumeTransactionCompleted").GetProperty("x-queue");
        JsonElement channel = Channel("analytics/asyncapi.v1.yaml", "transactionCompleted");

        Assert.Equal(MessagingTopology.LegacyQueue, queue.GetProperty("name").GetString());
        Assert.Equal(MessagingTopology.LegacyDeadLetterQueue, queue.GetProperty("deadLetterQueue").GetString());
        Assert.Equal(MessagingTopology.LegacyDeadLetterExchange, queue.GetProperty("deadLetterExchange").GetString());
        Assert.Equal(MessagingTopology.LegacyRoutingKey, channel.GetProperty("address").GetString());
        Assert.Equal(MessagingTopology.Exchange, ExchangeOf(channel));
    }

    [Theory]
    [InlineData("TransactionCompleted")]
    [InlineData("PartnerTransactionCompleted")]
    public void The_embedded_schemas_know_both_events(string messageType)
    {
        Assert.True(AsyncApiSchemas.FromEmbeddedContracts().Knows(messageType));
    }
}
