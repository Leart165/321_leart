using Analytics.Application.Abstractions;
using Analytics.Domain.Ledger;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Analytics.Infrastructure.Persistence;

public sealed class TotalsStore : ITotalsStore
{
    private readonly AnalyticsDbContext _context;

    public TotalsStore(AnalyticsDbContext context)
    {
        _context = context;
    }

    public async Task<bool> ApplyAsync(
        BookedTransaction transaction,
        LedgerContribution contribution,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(contribution);

        await using IDbContextTransaction databaseTransaction =
            await _context.Database.BeginTransactionAsync(cancellationToken);

        int inserted = await _context.Database.ExecuteSqlAsync(
            $@"INSERT INTO processed_transactions (transaction_id, processed_at)
               VALUES ({transaction.TransactionId}, {DateTimeOffset.UtcNow})
               ON CONFLICT (transaction_id) DO NOTHING",
            cancellationToken);

        if (inserted == 0)
        {
            await databaseTransaction.RollbackAsync(cancellationToken);
            return false;
        }

        await _context.Database.ExecuteSqlAsync(
            $@"INSERT INTO owner_monthly (owner_id, year, month, currency, income, expenses, transactions)
               VALUES ({transaction.OwnerId}, {transaction.Year}, {transaction.Month}, {transaction.Currency.Code},
                       {contribution.Income}, {contribution.Expenses}, 1)
               ON CONFLICT (owner_id, year, month, currency) DO UPDATE SET
                   income = owner_monthly.income + EXCLUDED.income,
                   expenses = owner_monthly.expenses + EXCLUDED.expenses,
                   transactions = owner_monthly.transactions + EXCLUDED.transactions",
            cancellationToken);

        await _context.Database.ExecuteSqlAsync(
            $@"INSERT INTO system_daily (day, currency, volume, deposits, withdrawals, transfers)
               VALUES ({transaction.Day}, {transaction.Currency.Code}, {contribution.Volume},
                       {contribution.Deposits}, {contribution.Withdrawals}, {contribution.Transfers})
               ON CONFLICT (day, currency) DO UPDATE SET
                   volume = system_daily.volume + EXCLUDED.volume,
                   deposits = system_daily.deposits + EXCLUDED.deposits,
                   withdrawals = system_daily.withdrawals + EXCLUDED.withdrawals,
                   transfers = system_daily.transfers + EXCLUDED.transfers",
            cancellationToken);

        await databaseTransaction.CommitAsync(cancellationToken);
        return true;
    }
}
