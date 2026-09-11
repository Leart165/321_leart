using System.Net;
using System.Net.Http.Json;
using Auth.Application.Abstractions;
using Auth.Domain.Exceptions;
using Auth.Domain.Transactions;

namespace Auth.Infrastructure.AccountsApi;

public sealed class AccountsApiClient : IAccountsApiClient
{
    private readonly HttpClient _httpClient;

    public AccountsApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IReadOnlyList<Guid>> GetAccountIdsByOwnerAsync(string ownerId, CancellationToken cancellationToken)
    {
        string requestUri = $"/v1/accounts?ownerId={Uri.EscapeDataString(ownerId)}";

        using HttpResponseMessage response = await SendAsync(requestUri, cancellationToken);

        List<AccountApiDto>? accounts = await response.Content.ReadFromJsonAsync<List<AccountApiDto>>(cancellationToken: cancellationToken);

        return accounts?.Select(account => account.Id).ToList() ?? new List<Guid>();
    }

    public async Task<IReadOnlyList<Transaction>> GetTransactionsByAccountAsync(Guid accountId, CancellationToken cancellationToken)
    {
        string requestUri = $"/v1/accounts/{accountId}/transactions";

        using HttpResponseMessage response = await SendAsync(requestUri, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return Array.Empty<Transaction>();
        }

        List<TransactionApiDto>? transactions = await response.Content.ReadFromJsonAsync<List<TransactionApiDto>>(cancellationToken: cancellationToken);

        return transactions?.Select(Map).ToList() ?? new List<Transaction>();
    }

    private async Task<HttpResponseMessage> SendAsync(string requestUri, CancellationToken cancellationToken)
    {
        HttpResponseMessage response;
        try
        {
            response = await _httpClient.GetAsync(requestUri, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            throw new UpstreamServiceException($"Der Kontendienst ist nicht erreichbar: {exception.Message}");
        }

        if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.NotFound)
        {
            response.Dispose();
            throw new UpstreamServiceException($"Der Kontendienst antwortete mit Status {(int)response.StatusCode}.");
        }

        return response;
    }

    private static Transaction Map(TransactionApiDto dto)
    {
        TransactionKind kind = Enum.Parse<TransactionKind>(dto.Kind, ignoreCase: true);

        return new Transaction(dto.Id, dto.AccountId, kind, dto.Amount, dto.Currency, dto.Description, dto.BookedAt);
    }
}
