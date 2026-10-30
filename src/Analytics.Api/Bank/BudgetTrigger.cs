using Analytics.Api.Middleware;
using Analytics.Application.Budgets;
using Analytics.Infrastructure.Persistence;

namespace Analytics.Api.Bank;

// Stösst nach einem Abgleich mit neuen Buchungen die Budget-Prüfung an. Ein Vorrat wie das
// Speichern: ist die eigene Datenbank gerade weg, bekommt der Kunde seine Zahlen trotzdem, und der
// nächste Abgleich prüft.
public sealed class BudgetTrigger
{
    private readonly BudgetService _budgets;
    private readonly IHttpContextAccessor _context;
    private readonly ILogger<BudgetTrigger> _logger;

    public BudgetTrigger(BudgetService budgets, IHttpContextAccessor context, ILogger<BudgetTrigger> logger)
    {
        _budgets = budgets;
        _context = context;
        _logger = logger;
    }

    public async Task AfterBookingsAsync(string ownerId, int newBookings, CancellationToken cancellationToken)
    {
        if (newBookings <= 0)
        {
            return;
        }

        string correlationId = _context.HttpContext is { } current ? CorrelationId.Of(current) : Guid.NewGuid().ToString();
        try
        {
            await _budgets.BookingsStoredAsync(ownerId, newBookings, correlationId, cancellationToken);
        }
        catch (Exception exception) when (DatabaseFailures.IsTransient(exception))
        {
            _logger.LogWarning(
                "Budget-Prüfung für {Owner} nicht angestossen, die Datenbank ist nicht erreichbar ({Error}).",
                ownerId, exception.GetBaseException().Message);
        }
    }
}
