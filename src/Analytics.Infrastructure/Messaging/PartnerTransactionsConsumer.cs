using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Analytics.Infrastructure.Messaging;

// Liest partner.transaction.completed aus der eigenen Queue analytics.partner, siehe
// contracts/analytics/asyncapi.v2.yaml.
public sealed class PartnerTransactionsConsumer : QueueConsumer
{
    public PartnerTransactionsConsumer(
        IServiceScopeFactory scopes,
        RabbitMqConnection connection,
        IOptions<MessagingOptions> options,
        ILogger<PartnerTransactionsConsumer> logger)
        : base(scopes, connection, options, logger)
    {
    }

    protected override string Queue
    {
        get { return MessagingTopology.PartnerQueue; }
    }

    protected override BookingFormat Format
    {
        get { return BookingFormat.Partner; }
    }

    protected override async Task<bool> PrepareAsync(IConnection connection, IChannel channel, CancellationToken cancellationToken)
    {
        await MessagingTopology.DeclarePartnerQueueAsync(channel, cancellationToken);
        return true;
    }
}
