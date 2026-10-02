using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Analytics.Tests.Api;

// Die eigene Datenbank antwortet nicht. Das ist kein Fehler des Aufrufers und keiner im Code:
// 503 mit Retry-After statt 500, und /health meldet den Dienst als nicht bereit.
public sealed class DatabaseOutageTests : IDisposable
{
    private const string Unreachable = "Host=127.0.0.1;Port=1;Database=analytics;Username=analytics;Password=analytics;Timeout=1";

    private readonly TestTokenIssuer _tokens = new TestTokenIssuer();
    private readonly AnalyticsApiFactory _api;

    public DatabaseOutageTests()
    {
        _api = new AnalyticsApiFactory(Unreachable, _tokens, migrate: false);
    }

    public void Dispose()
    {
        _api.Dispose();
        _tokens.Dispose();
    }

    [Fact]
    public async Task The_api_answers_503_with_retry_after_and_the_correlation_id()
    {
        HttpClient client = _api.ClientWith(_tokens.Customer());
        client.DefaultRequestHeaders.Add("X-Correlation-Id", "ausfall-1");

        HttpResponseMessage response = await client.GetAsync("/v1/analytics/me/monthly?year=2026");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(TimeSpan.FromSeconds(2), response.Headers.RetryAfter?.Delta);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        JsonElement problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("ausfall-1", problem.GetProperty("correlationId").GetString());
    }

    [Fact]
    public async Task Health_reports_the_service_as_not_ready()
    {
        HttpResponseMessage response = await _api.CreateClient().GetAsync("/health");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        JsonElement report = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Unhealthy", report.GetProperty("status").GetString());
    }
}
