using RabbitMQ.Client;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Xunit;

namespace Analytics.Tests.Messaging;

// Ein echter RabbitMQ, aber ein eigener virtueller Host je Testklasse: nichts, was hier passiert,
// berührt die Queues der Entwicklungsumgebung. Die Bank-Seite (bank.events, bank.dlx) legt die
// Fixture als Administrator an, der Dienst selbst läuft als eingeschränkter Benutzer mit genau den
// Rechten aus rabbitmq/partner-access.sh in m321-main.
public sealed class BrokerFixture : IAsyncLifetime
{
    private static readonly string Host = Environment.GetEnvironmentVariable("ANALYTICS_TEST_RABBITMQ_HOST") ?? "localhost";
    private static readonly string ManagementUrl = Environment.GetEnvironmentVariable("ANALYTICS_TEST_RABBITMQ_MANAGEMENT") ?? "http://localhost:15672";

    private const string AdminUser = "guest";
    private const string AdminPassword = "guest";

    private readonly HttpClient _management = new HttpClient();

    public string VirtualHost { get; } = "analytics-test-" + Guid.NewGuid().ToString("N");

    public string PartnerUser { get; } = "analytics-test-" + Guid.NewGuid().ToString("N")[..8];

    public string PartnerPassword { get; } = "analytics-test";

    public string BrokerHost
    {
        get { return Host; }
    }

    public async Task InitializeAsync()
    {
        _management.BaseAddress = new Uri(ManagementUrl);
        _management.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes($"{AdminUser}:{AdminPassword}")));

        string vhost = Uri.EscapeDataString(VirtualHost);
        await PutAsync($"api/vhosts/{vhost}", new { });
        await PutAsync($"api/permissions/{vhost}/{AdminUser}", new { configure = ".*", write = ".*", read = ".*" });

        // Die Bank-Seite zuerst: eine Topic-Berechtigung verlangt einen bestehenden Exchange.
        await using (IConnection admin = await ConnectWhenReadyAsync())
        await using (IChannel channel = await admin.CreateChannelAsync())
        {
            await channel.ExchangeDeclareAsync("bank.events", ExchangeType.Topic, durable: true, autoDelete: false);
            await channel.ExchangeDeclareAsync("bank.dlx", ExchangeType.Direct, durable: true, autoDelete: false);
        }

        await PutAsync($"api/users/{PartnerUser}", new { password = PartnerPassword, tags = "" });
        await PutAsync($"api/permissions/{vhost}/{PartnerUser}", new
        {
            configure = "^analytics\\..*",
            write = "^analytics\\..*",
            read = "^analytics\\..*|^bank\\.events$"
        });
        await GrantTopicPermissionAsync();
    }

    // Eine Topic-Berechtigung hängt am Exchange; wird bank.events neu angelegt, braucht es sie neu.
    public Task GrantTopicPermissionAsync()
    {
        return PutAsync($"api/topic-permissions/{Uri.EscapeDataString(VirtualHost)}/{PartnerUser}", new
        {
            exchange = "bank.events",
            write = "^$",
            read = "^(transaction\\.completed|partner\\..+)$"
        });
    }

    public async Task<JsonElement> GetManagementAsync(string path)
    {
        return await _management.GetFromJsonAsync<JsonElement>(path.Replace("{vhost}", Uri.EscapeDataString(VirtualHost)));
    }

    // Ein eben gestarteter Broker beantwortet die Management-API, bevor er AMQP annimmt, und ein
    // neuer virtueller Host ist nicht sofort offen. In der CI startet der Broker jedes Mal frisch.
    private async Task<IConnection> ConnectWhenReadyAsync()
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(20);
        while (true)
        {
            try
            {
                return await ConnectAsAdminAsync();
            }
            catch (Exception) when (DateTime.UtcNow < deadline)
            {
                await Task.Delay(500);
            }
        }
    }

    public Task<IConnection> ConnectAsAdminAsync()
    {
        return Factory(AdminUser, AdminPassword).CreateConnectionAsync();
    }

    public Task<IConnection> ConnectAsPartnerAsync()
    {
        return Factory(PartnerUser, PartnerPassword).CreateConnectionAsync();
    }

    public async Task DisposeAsync()
    {
        await _management.DeleteAsync($"api/vhosts/{Uri.EscapeDataString(VirtualHost)}");
        await _management.DeleteAsync($"api/users/{PartnerUser}");
        _management.Dispose();
    }

    private ConnectionFactory Factory(string user, string password)
    {
        return new ConnectionFactory
        {
            HostName = Host,
            UserName = user,
            Password = password,
            VirtualHost = VirtualHost
        };
    }

    private async Task PutAsync(string path, object body)
    {
        HttpResponseMessage response = await _management.PutAsJsonAsync(path, body);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"RabbitMQ unter {ManagementUrl} lehnt {path} ab: {(int)response.StatusCode}. Läuft er? docker compose up -d rabbitmq");
        }
    }
}
