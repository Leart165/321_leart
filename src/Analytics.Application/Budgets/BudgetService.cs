using Analytics.Application.Abstractions;
using Analytics.Application.Bank;
using Analytics.Application.Ledger;
using Analytics.Application.Reports;
using Analytics.Domain.Budgets;
using Analytics.Domain.Ledger;

namespace Analytics.Application.Budgets;

// Das Budget für die Ausgaben eines Monats und die Warnungen, wenn es zu 80 und zu 100 Prozent
// ausgeschöpft ist.
//
// Geprüft wird im Hintergrund, im Konsumenten der Queue analytics.budgets: immer wenn neue
// Buchungen von der Bank gespeichert wurden und wenn der Kunde sein Budget ändert. Die Anfrage des
// Kunden wartet nie auf die Prüfung.
//
// Gerechnet wird mit den gespeicherten Buchungen; sie stammen von der Bank, mit Zustimmung des
// Kunden. Fehlt die Zustimmung, zeigt der Dienst auch das Budget nicht.
public sealed class BudgetService
{
    private readonly IBudgetRepository _budgets;
    private readonly IBudgetEvents _events;
    private readonly IBookingLogReader _bookings;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _clock;

    public BudgetService(
        IBudgetRepository budgets,
        IBudgetEvents events,
        IBookingLogReader bookings,
        IUnitOfWork unitOfWork,
        TimeProvider clock)
    {
        _budgets = budgets;
        _events = events;
        _bookings = bookings;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<BudgetStatus> GetStatusAsync(string ownerId, bool consented, CancellationToken cancellationToken)
    {
        RequireConsent(consented);

        MonthlyBudget budget = await _budgets.FindAsync(ownerId, cancellationToken)
            ?? throw new BudgetNotFoundException();

        DateOnly today = Today();
        decimal spent = await SpentAsync(budget, today.Year, today.Month, cancellationToken);
        IReadOnlyList<BudgetAlert> alerts = await _budgets.ListAlertsAsync(ownerId, today.Year, today.Month, cancellationToken);

        return new BudgetStatus(budget, today.Year, today.Month, spent, budget.RatioOf(spent), budget.LevelFor(spent), alerts);
    }

    // Legt das Budget an oder ändert es, und lässt den laufenden Monat gleich prüfen.
    public async Task<BudgetStatus> SetAsync(
        string ownerId,
        bool consented,
        string currency,
        decimal limit,
        string correlationId,
        CancellationToken cancellationToken)
    {
        RequireConsent(consented);

        DateTimeOffset now = _clock.GetUtcNow();
        MonthlyBudget? budget = await _budgets.FindAsync(ownerId, cancellationToken);
        if (budget is null)
        {
            budget = MonthlyBudget.Set(ownerId, Currency.Of(currency), limit, now);
            _budgets.Add(budget);
        }
        else
        {
            budget.Change(Currency.Of(currency), limit, now);
        }

        DateOnly today = Today();
        _events.CheckRequested(budget.OwnerId, today.Year, today.Month, correlationId, now);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await GetStatusAsync(budget.OwnerId, consented, cancellationToken);
    }

    public async Task RemoveAsync(string ownerId, bool consented, CancellationToken cancellationToken)
    {
        RequireConsent(consented);

        MonthlyBudget budget = await _budgets.FindAsync(ownerId, cancellationToken)
            ?? throw new BudgetNotFoundException();

        _budgets.Remove(budget);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    // Nach neu gespeicherten Buchungen: eine Prüfung anstossen, aber nur, wenn der Kunde ein Budget
    // hat. Ein Vorrat wie das Speichern selbst; scheitert es, prüft der nächste Abgleich.
    public async Task BookingsStoredAsync(string ownerId, int newBookings, string correlationId, CancellationToken cancellationToken)
    {
        if (newBookings <= 0 || !await _budgets.ExistsAsync(ownerId, cancellationToken))
        {
            return;
        }

        DateOnly today = Today();
        _events.CheckRequested(ownerId, today.Year, today.Month, correlationId, _clock.GetUtcNow());
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    // Die Arbeit des Konsumenten. Idempotent: eine Stufe, die in diesem Monat schon gemeldet ist,
    // wird nicht noch einmal gemeldet. Prüfen zwei Instanzen gleichzeitig, verhindert der
    // eindeutige Schlüssel die zweite Warnung.
    public async Task<BudgetCheck> CheckAsync(string ownerId, int year, int month, CancellationToken cancellationToken)
    {
        MonthlyBudget? budget = await _budgets.FindAsync(ownerId, cancellationToken);
        if (budget is null)
        {
            return BudgetCheck.NoBudget;
        }

        decimal spent = await SpentAsync(budget, year, month, cancellationToken);
        BudgetLevel level = budget.LevelFor(spent);

        IReadOnlyList<BudgetAlert> existing = await _budgets.ListAlertsAsync(ownerId, year, month, cancellationToken);
        List<BudgetLevel> reached = new List<BudgetLevel>();
        if (level >= BudgetLevel.Warning)
        {
            reached.Add(BudgetLevel.Warning);
        }

        if (level == BudgetLevel.Exceeded)
        {
            reached.Add(BudgetLevel.Exceeded);
        }

        List<BudgetLevel> missing = reached.Where(wanted => existing.All(alert => alert.Level != wanted)).ToList();
        if (missing.Count == 0)
        {
            return BudgetCheck.Unchanged;
        }

        DateTimeOffset now = _clock.GetUtcNow();
        foreach (BudgetLevel wanted in missing)
        {
            _budgets.AddAlert(BudgetAlert.Raise(budget, year, month, wanted, spent, now));
        }

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrentChangeException)
        {
            return BudgetCheck.Unchanged;
        }

        return BudgetCheck.Raised;
    }

    private async Task<decimal> SpentAsync(MonthlyBudget budget, int year, int month, CancellationToken cancellationToken)
    {
        DateOnly first = new DateOnly(year, month, 1);
        IReadOnlyList<BookingEntry> bookings = await _bookings.GetForOwnerAsync(
            budget.OwnerId, first, first.AddMonths(1).AddDays(-1), int.MaxValue, cancellationToken);

        return bookings
            .Where(booking => booking.Currency == budget.Currency.Code)
            .Select(booking => LedgerContribution.Of(booking.ToTransaction(budget.OwnerId)))
            .Sum(contribution => contribution.Expenses);
    }

    private DateOnly Today()
    {
        return DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);
    }

    private static void RequireConsent(bool consented)
    {
        if (!consented)
        {
            throw BankAccessDeniedException.WithoutConsent();
        }
    }
}
