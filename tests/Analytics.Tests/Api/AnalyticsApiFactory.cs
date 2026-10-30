using Analytics.Infrastructure.Messaging;
using Analytics.Tests.Bank;
using Analytics.Infrastructure.Outbox;
using Analytics.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Analytics.Tests.Api;

// Die echte API gegen eine frische Testdatenbank, mit Test-Token und nachgebildeter Bank.
// Konsumenten und Outbox laufen nicht mit: sie würden sonst am Broker der Entwicklungsumgebung
// Nachrichten abholen und senden.
public sealed class AnalyticsApiFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;
    private readonly TestTokenIssuer _tokens;
    private readonly bool _migrate;

    public FakeStatementsApi Bank { get; } = new FakeStatementsApi();

    public AnalyticsApiFactory(string connectionString, TestTokenIssuer tokens, bool migrate = true)
    {
        _connectionString = connectionString;
        _tokens = tokens;
        _migrate = migrate;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:AnalyticsDb", _connectionString);
        builder.UseSetting(Analytics.Infrastructure.Observability.ObservabilityExtensions.EndpointVariable, string.Empty);

        _tokens.ConfigureApi(builder);

        // Kurze Geduld mit der Bank, damit ein Test mit hängender Bank nicht Sekunden wartet.
        builder.UseSetting("Bank:Resilience:RetryDelay", "00:00:00.010");
        builder.UseSetting("Bank:Resilience:AttemptTimeout", "00:00:00.300");
        builder.UseSetting("Bank:Resilience:TotalTimeout", "00:00:02");

        builder.ConfigureTestServices(services =>
        {
            Bank.Replace(services);

            List<ServiceDescriptor> consumers = services
                .Where(descriptor => descriptor.ServiceType == typeof(IHostedService)
                    && descriptor.ImplementationType is not null
                    && (descriptor.ImplementationType.IsSubclassOf(typeof(QueueConsumer))
                        || descriptor.ImplementationType == typeof(OutboxDispatcher)
                        || (!_migrate && descriptor.ImplementationType == typeof(DatabaseMigrator))))
                .ToList();
            foreach (ServiceDescriptor consumer in consumers)
            {
                services.Remove(consumer);
            }
        });
    }

    public HttpClient ClientWith(string token)
    {
        HttpClient client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
