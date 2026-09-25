using Analytics.Domain.Ledger;

namespace Analytics.Application.Abstractions;

public interface ITotalsStore
{
    Task<bool> ApplyAsync(
        BookedTransaction transaction,
        LedgerContribution contribution,
        CancellationToken cancellationToken);
}
