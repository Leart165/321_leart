using Analytics.Domain.Ledger;

namespace Analytics.Application.Ledger;

// Summen aus einzelnen Buchungen. Wie eine Buchung beiträgt, entscheidet LedgerContribution im
// Domain; hier wird nur gruppiert und addiert.
public static class LedgerTotals
{
    public static IReadOnlyList<MonthlyTotal> Monthly(IEnumerable<BookedTransaction> transactions)
    {
        return transactions
            .GroupBy(transaction => (transaction.Year, transaction.Month, transaction.Currency.Code))
            .OrderBy(group => group.Key.Year)
            .ThenBy(group => group.Key.Month)
            .ThenBy(group => group.Key.Code, StringComparer.Ordinal)
            .Select(group =>
            {
                List<LedgerContribution> contributions = group.Select(LedgerContribution.Of).ToList();
                return new MonthlyTotal(
                    group.Key.Year,
                    group.Key.Month,
                    group.Key.Code,
                    contributions.Sum(contribution => contribution.Income),
                    contributions.Sum(contribution => contribution.Expenses),
                    contributions.Count);
            })
            .ToList();
    }

    public static IReadOnlyList<DailyTotal> Daily(IEnumerable<BookedTransaction> transactions)
    {
        return transactions
            .GroupBy(transaction => (transaction.Day, transaction.Currency.Code))
            .OrderBy(group => group.Key.Day)
            .ThenBy(group => group.Key.Code, StringComparer.Ordinal)
            .Select(group =>
            {
                List<LedgerContribution> contributions = group.Select(LedgerContribution.Of).ToList();
                return new DailyTotal(
                    group.Key.Day,
                    group.Key.Code,
                    contributions.Sum(contribution => contribution.Volume),
                    contributions.Sum(contribution => contribution.Deposits),
                    contributions.Sum(contribution => contribution.Withdrawals),
                    contributions.Sum(contribution => contribution.Transfers));
            })
            .ToList();
    }

    public static BookedTransaction ToTransaction(this BookingEntry entry, string ownerId)
    {
        return BookedTransaction.Of(
            entry.TransactionId,
            ownerId,
            Enum.Parse<TransactionKind>(entry.Kind),
            entry.Amount,
            Currency.Of(entry.Currency),
            entry.BookedAt);
    }
}
