using Analytics.Infrastructure.Messaging;
using Analytics.Infrastructure.Persistence;
using Analytics.Tests.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Analytics.Tests.Api;

// /v2/analytics/me/budget: festlegen, Stand im laufenden Monat, löschen. Nur das eigene Budget,
// nur mit Zustimmung für die Bank.
public sealed class BudgetTests : IClassFixture<PostgresFixture>, IAsyncLifetime, IDisposable
{
    private const string Budget = "/v2/analytics/me/budget";

    private readonly PostgresFixture _postgres;
    private readonly TestTokenIssuer _tokens = new TestTokenIssuer();
    private readonly AnalyticsApiFactory _api;

    public BudgetTests(PostgresFixture postgres)
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
    public async Task A_budget_is_set_shown_and_removed()
    {
        HttpClient client = _api.ClientWith(_tokens.Customer());

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(Budget)).StatusCode);

        HttpResponseMessage put = await client.PutAsJsonAsync(Budget, new { currency = "CHF", monthlyLimit = 1000m });
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        JsonElement budget = await put.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("CHF", budget.GetProperty("currency").GetString());
        Assert.Equal(1000m, budget.GetProperty("monthlyLimit").GetDecimal());
        Assert.Equal("ok", budget.GetProperty("level").GetString());
        Assert.Equal(0, budget.GetProperty("percent").GetInt32());

        HttpResponseMessage get = await client.GetAsync(Budget);
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync(Budget)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(Budget)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync(Budget)).StatusCode);
    }

    // Neue Buchungen von der Bank stossen eine Prüfung über die Queue analytics.budgets an; den
    // Stand rechnet GET sofort.
    [Fact]
    public async Task New_bookings_from_the_bank_ask_for_a_check_and_the_level_follows_the_expenses()
    {
        HttpClient client = _api.ClientWith(_tokens.Customer());
        await client.PutAsJsonAsync(Budget, new { currency = "CHF", monthlyLimit = 100m });
        DateTimeOffset now = DateTimeOffset.UtcNow;
        _api.Bank.Book(TestTokenIssuer.Subject, "Withdrawal", 85m, now.AddMinutes(-1));
        DateOnly today = DateOnly.FromDateTime(now.UtcDateTime);

        HttpResponseMessage bookings = await client.GetAsync($"/v2/analytics/me/bookings?from={today:yyyy-MM-dd}&to={today:yyyy-MM-dd}");
        Assert.Equal(HttpStatusCode.OK, bookings.StatusCode);

        await using (AnalyticsDbContext context = _postgres.CreateContext())
        {
            Assert.Equal(2, await context.Outbox.CountAsync(message => message.RoutingKey == BudgetTopology.CheckRequestedRoutingKey));
        }

        JsonElement budget = await client.GetFromJsonAsync<JsonElement>(Budget);
        Assert.Equal("warning", budget.GetProperty("level").GetString());
        Assert.Equal(85, budget.GetProperty("percent").GetInt32());
        Assert.Equal(85m, budget.GetProperty("spent").GetDecimal());
    }

    [Theory]
    [InlineData("{\"currency\":\"CHF\",\"monthlyLimit\":0}")]
    [InlineData("{\"currency\":\"CHF\",\"monthlyLimit\":-5}")]
    [InlineData("{\"currency\":\"CHF\",\"monthlyLimit\":10.005}")]
    [InlineData("{\"currency\":\"chf\",\"monthlyLimit\":100}")]
    [InlineData("{\"monthlyLimit\":100}")]
    [InlineData("{\"currency\":\"CHF\"}")]
    public async Task An_invalid_budget_is_a_bad_request(string body)
    {
        HttpResponseMessage response = await _api.ClientWith(_tokens.Customer()).PutAsync(
            Budget, new StringContent(body, System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using AnalyticsDbContext context = _postgres.CreateContext();
        Assert.Equal(0, await context.Budgets.CountAsync());
    }

    [Fact]
    public async Task Without_consent_for_the_bank_there_is_no_budget()
    {
        string token = _tokens.Issue(new TokenSpec { Scope = "openid analytics:read" });

        HttpResponseMessage response = await _api.ClientWith(token).PutAsJsonAsync(Budget, new { currency = "CHF", monthlyLimit = 100m });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("scope=\"statements:read\"", Assert.Single(response.Headers.GetValues("WWW-Authenticate")));
        await using AnalyticsDbContext context = _postgres.CreateContext();
        Assert.Equal(0, await context.Budgets.CountAsync());
    }

    [Fact]
    public async Task Every_customer_sees_only_the_own_budget()
    {
        await _api.ClientWith(_tokens.Customer()).PutAsJsonAsync(Budget, new { currency = "CHF", monthlyLimit = 100m });

        HttpResponseMessage response = await _api.ClientWith(_tokens.CustomerNamed("kunde-2")).GetAsync(Budget);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Without_token_it_is_401()
    {
        HttpResponseMessage response = await _api.CreateClient().GetAsync(Budget);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
