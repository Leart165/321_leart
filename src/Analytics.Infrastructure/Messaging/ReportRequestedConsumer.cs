using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Analytics.Infrastructure.Messaging;

// Liest report.requested aus der eigenen Queue analytics.reports und erzeugt das PDF. Beide
// Instanzen hängen an derselben Queue und teilen sich die Anträge.
public sealed class ReportRequestedConsumer : QueueConsumer
{
    public ReportRequestedConsumer(
        IServiceScopeFactory scopes,
        RabbitMqConnection connection,
        IOptions<MessagingOptions> options,
        ILogger<ReportRequestedConsumer> logger)
        : base(scopes, connection, options, logger)
    {
    }

    protected override string Queue
    {
        get { return ReportTopology.ReportsQueue; }
    }

    protected override async Task<bool> PrepareAsync(IConnection connection, IChannel channel, CancellationToken cancellationToken)
    {
        await ReportTopology.DeclareReportsQueueAsync(channel, cancellationToken);
        return true;
    }

    protected override Task<string> HandleAsync(IServiceProvider services, BasicDeliverEventArgs delivery)
    {
        ReportRequestedHandler handler = services.GetRequiredService<ReportRequestedHandler>();
        return handler.HandleAsync(delivery.Body, delivery.BasicProperties.Type, delivery.Redelivered, delivery.CancellationToken);
    }
}
