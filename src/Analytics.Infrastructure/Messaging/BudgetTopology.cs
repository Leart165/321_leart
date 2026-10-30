using RabbitMQ.Client;

namespace Analytics.Infrastructure.Messaging;

// Die Budget-Prüfung am eigenen Exchange analytics.events. Kontrakt:
// contracts/analytics/events.asyncapi.v1.yaml.
public static class BudgetTopology
{
    public const string CheckRequestedRoutingKey = "budget.check.requested";

    public const string CheckRequestedType = "BudgetCheckRequested";

    public const string BudgetsQueue = "analytics.budgets";

    public const string BudgetsDeadLetterQueue = "analytics.budgets.dlq";

    public static Task DeclareBudgetsQueueAsync(IChannel channel, CancellationToken cancellationToken)
    {
        return ReportTopology.DeclareQueueAsync(
            channel, BudgetsQueue, BudgetsDeadLetterQueue, CheckRequestedRoutingKey, cancellationToken);
    }
}
