using Analytics.Application.Abstractions;
using Analytics.Infrastructure.Messaging;
using Analytics.Infrastructure.Persistence;
using System.Diagnostics;
using System.Text.Json;

namespace Analytics.Infrastructure.Outbox;

// Schreibt budget.check.requested in die Outbox. Publiziert wird erst, wenn gespeichert ist.
public sealed class OutboxBudgetEvents : IBudgetEvents
{
    private const int SchemaVersion = 1;

    private static readonly JsonSerializerOptions SerializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);

    private readonly AnalyticsDbContext _context;

    public OutboxBudgetEvents(AnalyticsDbContext context)
    {
        _context = context;
    }

    public void CheckRequested(string ownerId, int year, int month, string correlationId, DateTimeOffset requestedAt)
    {
        string payload = JsonSerializer.Serialize(
            new BudgetCheckRequestedPayload(ownerId, year, month, requestedAt), SerializerOptions);

        _context.Outbox.Add(OutboxMessage.Create(
            BudgetTopology.CheckRequestedRoutingKey,
            BudgetTopology.CheckRequestedType,
            SchemaVersion,
            correlationId,
            Activity.Current?.Id,
            payload,
            requestedAt));
    }
}
