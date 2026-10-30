using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Analytics.Infrastructure.Messaging;

// Liest budget.check.requested aus der eigenen Queue analytics.budgets. Kommt oft: nach jedem
// Abgleich mit der Bank, der neue Buchungen bringt, und nach jeder Änderung eines Budgets.
public sealed class BudgetCheckConsumer : QueueConsumer
{
    public BudgetCheckConsumer(
        IServiceScopeFactory scopes,
        RabbitMqConnection connection,
        IOptions<MessagingOptions> options,
        ILogger<BudgetCheckConsumer> logger)
        : base(scopes, connection, options, logger)
    {
    }

    protected override string Queue
    {
        get { return BudgetTopology.BudgetsQueue; }
    }

    protected override async Task<bool> PrepareAsync(IConnection connection, IChannel channel, CancellationToken cancellationToken)
    {
        await BudgetTopology.DeclareBudgetsQueueAsync(channel, cancellationToken);
        return true;
    }

    protected override Task<string> HandleAsync(IServiceProvider services, BasicDeliverEventArgs delivery)
    {
        BudgetCheckHandler handler = services.GetRequiredService<BudgetCheckHandler>();
        return handler.HandleAsync(delivery.Body, delivery.Redelivered, delivery.CancellationToken);
    }
}
