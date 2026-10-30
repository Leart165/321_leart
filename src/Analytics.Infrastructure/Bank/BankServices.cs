using Analytics.Application.Bank;
using Analytics.Infrastructure.Bank.Generated;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Analytics.Infrastructure.Bank;

public static class BankServices
{
    // Gibt den Client zurück, damit die API ihre Handler einhängen kann: zuerst den, der das Token
    // weiterreicht, dann AddBankResilienceHandler.
    public static IHttpClientBuilder AddBankStatements(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<BankOptions>(configuration.GetSection(BankOptions.SectionName));
        services.Configure<BankResilienceOptions>(configuration.GetSection(BankResilienceOptions.SectionName));
        services.AddBankResilience();
        services.AddScoped<IBankBookings, StatementsBookings>();

        return services.AddHttpClient<StatementsApiClient>((provider, client) =>
        {
            client.BaseAddress = provider.GetRequiredService<IOptions<BankOptions>>().Value.BaseUrl;
        });
    }
}
