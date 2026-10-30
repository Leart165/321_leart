using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Analytics.Infrastructure.Messaging;

// Liest annual-report.requested aus der eigenen Queue analytics.annual-reports und erzeugt den
// Jahresauszug. Eigene Queue, damit ein grosser Auszug die Monatsberichte nicht aufhält; die
// Verarbeitung selbst ist dieselbe wie beim Monatsbericht.
public sealed class AnnualReportRequestedConsumer : QueueConsumer
{
    public AnnualReportRequestedConsumer(
        IServiceScopeFactory scopes,
        RabbitMqConnection connection,
        IOptions<MessagingOptions> options,
        ILogger<AnnualReportRequestedConsumer> logger)
        : base(scopes, connection, options, logger)
    {
    }

    protected override string Queue
    {
        get { return ReportTopology.AnnualReportsQueue; }
    }

    protected override async Task<bool> PrepareAsync(IConnection connection, IChannel channel, CancellationToken cancellationToken)
    {
        await ReportTopology.DeclareAnnualReportsQueueAsync(channel, cancellationToken);
        return true;
    }

    protected override Task<string> HandleAsync(IServiceProvider services, BasicDeliverEventArgs delivery)
    {
        ReportRequestedHandler handler = services.GetRequiredService<ReportRequestedHandler>();
        return handler.HandleAsync(
            delivery.Body, delivery.BasicProperties.Type, ReportTopology.AnnualReportRequestedType,
            delivery.Redelivered, delivery.CancellationToken);
    }
}
