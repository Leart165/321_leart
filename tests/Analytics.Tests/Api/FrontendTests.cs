using Analytics.Tests.Persistence;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Analytics.Tests.Api;

// Die Statistikseite kommt vom Dienst selbst, ohne Token, und lädt nur eigene Dateien.
public sealed class FrontendTests : IClassFixture<PostgresFixture>, IDisposable
{
    private readonly TestTokenIssuer _tokens = new TestTokenIssuer();
    private readonly AnalyticsApiFactory _api;

    public FrontendTests(PostgresFixture postgres)
    {
        _api = new AnalyticsApiFactory(postgres.ConnectionString, _tokens);
    }

    public void Dispose()
    {
        _api.Dispose();
        _tokens.Dispose();
    }

    [Fact]
    public async Task The_page_is_served_without_a_token()
    {
        HttpResponseMessage response = await _api.CreateClient().GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("js/main.js", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("/js/main.js", "text/javascript")]
    [InlineData("/css/app.css", "text/css")]
    public async Task Scripts_and_styles_are_served(string path, string mediaType)
    {
        HttpResponseMessage response = await _api.CreateClient().GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(mediaType, response.Content.Headers.ContentType?.MediaType);
    }

    // Nach einem neuen Image muss der Browser die neuen Module laden, nicht die aus seinem Cache.
    [Theory]
    [InlineData("/")]
    [InlineData("/js/views/bookingsView.js")]
    [InlineData("/css/app.css")]
    public async Task The_browser_revalidates_the_page_on_every_load(string path)
    {
        HttpResponseMessage response = await _api.CreateClient().GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoCache, $"{path} ohne Cache-Control: no-cache");
    }

    [Fact]
    public async Task The_config_names_the_keycloak_of_the_bank()
    {
        JsonElement config = await _api.CreateClient().GetFromJsonAsync<JsonElement>("/frontend/config.json");

        Assert.Equal(TestTokenIssuer.Issuer, config.GetProperty("issuer").GetString());
        Assert.Equal("analytics-web", config.GetProperty("clientId").GetString());
    }

    [Fact]
    public async Task The_page_only_talks_to_itself_and_the_keycloak()
    {
        HttpResponseMessage response = await _api.CreateClient().GetAsync("/");

        string policy = Assert.Single(response.Headers.GetValues("Content-Security-Policy"));
        Assert.Contains("script-src 'self'", policy);
        Assert.Contains("connect-src 'self' https://issuer.test", policy);
        Assert.Contains("frame-ancestors 'none'", policy);
        Assert.Equal("no-referrer", Assert.Single(response.Headers.GetValues("Referrer-Policy")));
    }

    [Fact]
    public async Task Swagger_keeps_working_without_the_page_policy()
    {
        HttpResponseMessage response = await _api.CreateClient().GetAsync("/swagger/index.html");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(response.Headers.Contains("Content-Security-Policy"));
    }
}
