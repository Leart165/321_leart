using Analytics.Application.Reports;
using Analytics.Infrastructure.Persistence;
using Analytics.Tests.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Analytics.Tests.Api;

// /v2/analytics/me/reports: beantragen mit 202, Status verfolgen, PDF abholen. Nur eigene
// Berichte, nur mit Scope.
public sealed class ReportsTests : IClassFixture<PostgresFixture>, IAsyncLifetime, IDisposable
{
    private const string Reports = "/v2/analytics/me/reports";

    private readonly PostgresFixture _postgres;
    private readonly TestTokenIssuer _tokens = new TestTokenIssuer();
    private readonly AnalyticsApiFactory _api;

    public ReportsTests(PostgresFixture postgres)
    {
        _postgres = postgres;
        _api = new AnalyticsApiFactory(postgres.ConnectionString, _tokens);
    }

    public Task InitializeAsync()
    {
        return _postgres.ResetAsync();
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _api.Dispose();
        _tokens.Dispose();
    }

    [Fact]
    public async Task A_request_is_accepted_with_202_and_points_to_its_status()
    {
        HttpClient client = _api.ClientWith(_tokens.Customer());
        client.DefaultRequestHeaders.Add("X-Correlation-Id", "demo-123");

        HttpResponseMessage response = await client.PostAsJsonAsync(Reports, new { year = 2026, month = 9 });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        JsonElement report = await response.Content.ReadFromJsonAsync<JsonElement>();
        Guid reportId = report.GetProperty("reportId").GetGuid();
        Assert.Equal("requested", report.GetProperty("status").GetString());
        Assert.Equal(2026, report.GetProperty("year").GetInt32());
        Assert.Equal(9, report.GetProperty("month").GetInt32());
        Assert.EndsWith($"{Reports}/{reportId}", response.Headers.Location!.ToString(), StringComparison.OrdinalIgnoreCase);

        await using AnalyticsDbContext context = _postgres.CreateContext();
        Assert.Equal("demo-123", (await context.Outbox.SingleAsync()).CorrelationId);
    }

    [Theory]
    [InlineData("{\"year\":2026,\"month\":13}")]
    [InlineData("{\"year\":1999,\"month\":1}")]
    [InlineData("{\"year\":2026}")]
    [InlineData("{\"year\":2099,\"month\":1}")]
    public async Task An_invalid_or_future_month_is_a_bad_request(string body)
    {
        HttpResponseMessage response = await _api.ClientWith(_tokens.Customer()).PostAsync(
            Reports, new StringContent(body, System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using AnalyticsDbContext context = _postgres.CreateContext();
        Assert.Equal(0, await context.Outbox.CountAsync());
    }

    [Fact]
    public async Task The_document_is_a_conflict_until_ready_and_then_a_pdf()
    {
        HttpClient client = _api.ClientWith(_tokens.Customer());
        Guid reportId = await RequestAsync(client);

        HttpResponseMessage early = await client.GetAsync($"{Reports}/{reportId}/document");
        Assert.Equal(HttpStatusCode.Conflict, early.StatusCode);

        await GenerateAsync(reportId);

        JsonElement status = await client.GetFromJsonAsync<JsonElement>($"{Reports}/{reportId}");
        Assert.Equal("ready", status.GetProperty("status").GetString());
        Assert.Equal(0, status.GetProperty("bookingCount").GetInt32());

        HttpResponseMessage document = await client.GetAsync($"{Reports}/{reportId}/document");
        Assert.Equal(HttpStatusCode.OK, document.StatusCode);
        Assert.Equal("application/pdf", document.Content.Headers.ContentType!.MediaType);
        Assert.Equal("buchungen-2026-09.pdf", document.Content.Headers.ContentDisposition!.FileName);
        byte[] pdf = await document.Content.ReadAsByteArrayAsync();
        Assert.Equal("%PDF-", System.Text.Encoding.ASCII.GetString(pdf, 0, 5));
    }

    [Fact]
    public async Task A_foreign_report_is_not_found_and_not_listed()
    {
        Guid foreign = await RequestAsync(_api.ClientWith(_tokens.CustomerNamed("jemand-anders")));
        await GenerateAsync(foreign);
        HttpClient client = _api.ClientWith(_tokens.Customer());
        Guid own = await RequestAsync(client);

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{Reports}/{foreign}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{Reports}/{foreign}/document")).StatusCode);

        JsonElement[] list = await client.GetFromJsonAsync<JsonElement[]>(Reports) ?? Array.Empty<JsonElement>();
        Assert.Equal(new[] { own }, list.Select(report => report.GetProperty("reportId").GetGuid()));
    }

    [Fact]
    public async Task Without_the_scope_requesting_is_forbidden()
    {
        string token = _tokens.Issue(new TokenSpec { Scope = "openid profile" });

        HttpResponseMessage response = await _api.ClientWith(token).PostAsJsonAsync(Reports, new { year = 2026, month = 9 });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("insufficient_scope", Assert.Single(response.Headers.GetValues("WWW-Authenticate")));
    }

    [Fact]
    public async Task Without_a_token_nothing_is_accepted()
    {
        HttpResponseMessage response = await _api.CreateClient().PostAsJsonAsync(Reports, new { year = 2026, month = 9 });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static async Task<Guid> RequestAsync(HttpClient client)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync(Reports, new { year = 2026, month = 9 });
        response.EnsureSuccessStatusCode();
        JsonElement report = await response.Content.ReadFromJsonAsync<JsonElement>();
        return report.GetProperty("reportId").GetGuid();
    }

    // Was sonst der Konsument der Queue analytics.reports tut.
    private async Task GenerateAsync(Guid reportId)
    {
        using IServiceScope scope = _api.Services.CreateScope();
        ReportGeneration result = await scope.ServiceProvider.GetRequiredService<ReportService>().GenerateAsync(reportId, CancellationToken.None);
        Assert.Equal(ReportGeneration.Generated, result);
    }
}
