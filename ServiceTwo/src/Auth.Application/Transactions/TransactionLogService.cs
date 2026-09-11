using Auth.Application.Abstractions;
using Auth.Domain.Exceptions;
using Auth.Domain.Transactions;

namespace Auth.Application.Transactions;

public sealed class TransactionLogService
{
    private readonly IAccountsApiClient _accountsApi;

    public TransactionLogService(IAccountsApiClient accountsApi)
    {
        _accountsApi = accountsApi;
    }

    public async Task<IReadOnlyList<Transaction>> GetTransactionLogAsync(string ownerId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(ownerId) || ownerId.Length > 100)
        {
            throw new DomainValidationException("ownerId ist erforderlich und darf höchstens 100 Zeichen umfassen.");
        }

        IReadOnlyList<Guid> accountIds = await _accountsApi.GetAccountIdsByOwnerAsync(ownerId, cancellationToken);

        Task<IReadOnlyList<Transaction>>[] fetches = accountIds
            .Select(accountId => _accountsApi.GetTransactionsByAccountAsync(accountId, cancellationToken))
            .ToArray();

        IReadOnlyList<Transaction>[] results = await Task.WhenAll(fetches);

        return results
            .SelectMany(transactions => transactions)
            .OrderByDescending(transaction => transaction.BookedAt)
            .ToList();
    }
}
