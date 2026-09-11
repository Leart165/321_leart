using Auth.Domain.Transactions;

namespace Auth.Application.Abstractions;

public interface IAccountsApiClient
{
    Task<IReadOnlyList<Guid>> GetAccountIdsByOwnerAsync(string ownerId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Transaction>> GetTransactionsByAccountAsync(Guid accountId, CancellationToken cancellationToken);
}
