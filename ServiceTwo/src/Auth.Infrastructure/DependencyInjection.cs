using Auth.Application.Abstractions;
using Auth.Application.Transactions;
using Auth.Infrastructure.AccountsApi;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Auth.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddTransactionsCore(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AccountsApiOptions>(configuration.GetSection(AccountsApiOptions.SectionName));

        services.AddHttpClient<IAccountsApiClient, AccountsApiClient>((provider, client) =>
        {
            AccountsApiOptions options = provider.GetRequiredService<IOptions<AccountsApiOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl);
        });

        services.AddScoped<TransactionLogService>();

        return services;
    }
}
