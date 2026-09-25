using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using System.Net;
using Xunit;

namespace Analytics.Tests.Api;

public sealed class AccessTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly string TestConnectionString =
        Environment.GetEnvironmentVariable("ANALYTICS_TEST_CONNECTION")
        ?.Replace("Database=postgres", "Database=analytics")
        ?? "Host=localhost;Port=5435;Database=analytics;Username=analytics;Password=analytics";

    private readonly WebApplicationFactory<Program> _factory;

    public AccessTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:AnalyticsDb"] = TestConnectionString
                })));
    }

    [Fact]
    public async Task MyMonthly_without_a_token_is_unauthorized()
    {
        using HttpClient client = _factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/v1/analytics/me/monthly?year=2026");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SystemDaily_without_a_token_is_unauthorized()
    {
        using HttpClient client = _factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(
            "/v1/analytics/system/daily?from=2026-09-01&to=2026-09-30");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Health_is_reachable_without_a_token()
    {
        using HttpClient client = _factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
