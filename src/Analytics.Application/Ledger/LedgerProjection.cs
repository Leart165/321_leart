using Analytics.Application.Abstractions;
using Analytics.Domain.Ledger;

namespace Analytics.Application.Ledger;

public sealed class LedgerProjection
{
    private readonly ITotalsStore _totals;

    public LedgerProjection(ITotalsStore totals)
    {
        _totals = totals;
    }

    public Task<bool> ApplyAsync(BookedTransaction transaction, CancellationToken cancellationToken)
    {
        LedgerContribution contribution = LedgerContribution.Of(transaction);
        return _totals.ApplyAsync(transaction, contribution, cancellationToken);
    }
}
