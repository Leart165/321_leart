using Analytics.Infrastructure.Messaging;
using System.Text.Json;
using Xunit;

namespace Analytics.Tests.Contracts;

// Die Namen am eigenen Broker müssen genau die aus contracts/analytics/events.asyncapi.v1.yaml sein.
public sealed class MessagingContractTests
{
    private const string Contract = "analytics/events.asyncapi.v1.yaml";

    private static JsonElement Channel(string channel)
    {
        using JsonDocument document = ContractFile.Load(Contract);
        return document.RootElement.GetProperty("channels").GetProperty(channel).Clone();
    }

    [Fact]
    public void The_report_request_goes_to_the_exchange_and_queue_of_the_contract()
    {
        JsonElement published = Channel("reportRequested");
        JsonElement queue = Channel("reportsQueue");

        Assert.Equal(ReportTopology.ReportRequestedRoutingKey, published.GetProperty("address").GetString());
        Assert.Equal(ReportTopology.Exchange,
            published.GetProperty("bindings").GetProperty("amqp").GetProperty("exchange").GetProperty("name").GetString());
        Assert.Equal(ReportTopology.ReportsQueue, queue.GetProperty("address").GetString());
        Assert.Equal(MessagingTopology.DeadLetterExchange, queue.GetProperty("x-deadLetterExchange").GetString());
        Assert.Equal(ReportTopology.ReportsDeadLetterQueue, queue.GetProperty("x-deadLetterQueue").GetString());
    }

    [Theory]
    [InlineData("annualReportRequested", "annualReportsQueue", ReportTopology.AnnualReportRequestedRoutingKey,
        ReportTopology.AnnualReportsQueue, ReportTopology.AnnualReportsDeadLetterQueue)]
    [InlineData("budgetCheckRequested", "budgetsQueue", BudgetTopology.CheckRequestedRoutingKey,
        BudgetTopology.BudgetsQueue, BudgetTopology.BudgetsDeadLetterQueue)]
    public void The_further_queues_are_named_as_in_the_contract(
        string publishedChannel, string queueChannel, string routingKey, string queueName, string deadLetterQueue)
    {
        JsonElement published = Channel(publishedChannel);
        JsonElement queue = Channel(queueChannel);

        Assert.Equal(routingKey, published.GetProperty("address").GetString());
        Assert.Equal(ReportTopology.Exchange,
            published.GetProperty("bindings").GetProperty("amqp").GetProperty("exchange").GetProperty("name").GetString());
        Assert.Equal(queueName, queue.GetProperty("address").GetString());
        Assert.Equal(MessagingTopology.DeadLetterExchange, queue.GetProperty("x-deadLetterExchange").GetString());
        Assert.Equal(deadLetterQueue, queue.GetProperty("x-deadLetterQueue").GetString());
    }

    [Fact]
    public void Everything_this_service_creates_starts_with_analytics()
    {
        string[] created =
        {
            ReportTopology.Exchange,
            ReportTopology.ReportsQueue,
            ReportTopology.ReportsDeadLetterQueue,
            ReportTopology.AnnualReportsQueue,
            ReportTopology.AnnualReportsDeadLetterQueue,
            BudgetTopology.BudgetsQueue,
            BudgetTopology.BudgetsDeadLetterQueue,
            MessagingTopology.DeadLetterExchange
        };

        Assert.All(created, name => Assert.StartsWith("analytics.", name, StringComparison.Ordinal));
    }

    // Nachrichten der Bank liest der Dienst nicht mehr; ihr Schema ist nicht mehr eingebettet.
    [Fact]
    public void The_embedded_schemas_know_only_the_own_events()
    {
        AsyncApiSchemas schemas = AsyncApiSchemas.FromEmbeddedContracts();

        Assert.True(schemas.Knows(ReportTopology.ReportRequestedType));
        Assert.True(schemas.Knows(ReportTopology.AnnualReportRequestedType));
        Assert.True(schemas.Knows(BudgetTopology.CheckRequestedType));
        Assert.False(schemas.Knows("PartnerTransactionCompleted"));
    }
}
