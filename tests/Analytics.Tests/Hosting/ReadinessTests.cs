using Analytics.Api.Hosting;
using Analytics.Tests.Api;
using Analytics.Tests.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Net;
using Xunit;

namespace Analytics.Tests.Hosting;

// /ready antwortet nur auf dem internen Port und meldet beim Herunterfahren sofort 503, damit
// Traefik die Instanz herausnimmt, bevor sie wirklich stoppt.
public sealed class ReadinessTests : IClassFixture<PostgresFixture>, IDisposable
{
    private readonly TestTokenIssuer _tokens = new TestTokenIssuer();
    private readonly AnalyticsApiFactory _api;

    public ReadinessTests(PostgresFixture postgres)
    {
        _api = new AnalyticsApiFactory(postgres.ConnectionString, _tokens);
    }

    public void Dispose()
    {
        _api.Dispose();
        _tokens.Dispose();
    }

    [Fact]
    public async Task Ready_answers_on_the_internal_port()
    {
        HttpResponseMessage response = await Internal().GetAsync(Readiness.Path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("ready", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Ready_is_not_reachable_on_the_api_port()
    {
        HttpResponseMessage response = await _api.CreateClient().GetAsync(Readiness.Path);

        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task While_draining_ready_answers_503()
    {
        _api.Services.GetRequiredService<ReadinessState>().MarkDraining();

        HttpResponseMessage response = await Internal().GetAsync(Readiness.Path);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task Stopping_marks_the_instance_as_draining_at_once()
    {
        ReadinessState state = new ReadinessState();
        DrainOnShutdown drain = new DrainOnShutdown(
            state, Options.Create(new ReadinessOptions()), NullLogger<DrainOnShutdown>.Instance);

        await drain.StoppingAsync(CancellationToken.None);

        Assert.False(state.IsReady);
    }

    private HttpClient Internal()
    {
        HttpClient client = _api.CreateClient();
        client.DefaultRequestHeaders.Host = "localhost:8081";
        return client;
    }
}
