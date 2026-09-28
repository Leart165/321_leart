using Analytics.Tests.Persistence;
using System.Net;
using Xunit;

namespace Analytics.Tests.Api;

// Hinter Traefik liegt die Oberfläche unter /analytics/swagger/, der Dienst selbst sieht aber
// /swagger/. Nur ein relativer Verweis auf die Spezifikation funktioniert in beiden Fällen.
public sealed class SwaggerTests : IClassFixture<PostgresFixture>, IDisposable
{
    private readonly TestTokenIssuer _tokens = new TestTokenIssuer();
    private readonly AnalyticsApiFactory _api;

    public SwaggerTests(PostgresFixture postgres)
    {
        _api = new AnalyticsApiFactory(postgres.ConnectionString, _tokens);
    }

    public void Dispose()
    {
        _api.Dispose();
        _tokens.Dispose();
    }

    [Fact]
    public async Task The_ui_refers_to_the_specification_relatively()
    {
        HttpClient client = _api.CreateClient();

        string configuration = await client.GetStringAsync("/swagger/index.js");

        Assert.Contains("\"url\":\"v1/swagger.json\"", configuration.Replace(" ", string.Empty));
        Assert.DoesNotContain("/swagger/v1/swagger.json", configuration);
    }

    [Fact]
    public async Task The_specification_is_served_next_to_the_ui()
    {
        HttpResponseMessage response = await _api.CreateClient().GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
