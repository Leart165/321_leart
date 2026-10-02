using Analytics.Application.Ledger;
using Analytics.Domain.Ledger;
using Analytics.Infrastructure.Persistence;
using Analytics.Tests.Persistence;
using Microsoft.IdentityModel.Tokens;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Analytics.Tests.Api;

// Ein Zugriff braucht drei Dinge (contracts/auth/scopes.md): ein gültiges Token mit dem Scope
// analytics:read, für system/daily die Rolle bank-admin, und die Besitzregel sub gleich ownerId.
public sealed class AccessTests : IClassFixture<PostgresFixture>, IDisposable
{
    private const string InsufficientScope = "Bearer error=\"insufficient_scope\", scope=\"analytics:read\"";

    private const string MyMonthly = "/v1/analytics/me/monthly?year=2026";
    private const string SystemDaily = "/v1/analytics/system/daily?from=2026-09-01&to=2026-09-30";

    private readonly TestTokenIssuer _tokens = new TestTokenIssuer();
    private readonly PostgresFixture _postgres;
    private readonly AnalyticsApiFactory _api;

    public AccessTests(PostgresFixture postgres)
    {
        _postgres = postgres;
        _api = new AnalyticsApiFactory(postgres.ConnectionString, _tokens);
    }

    public void Dispose()
    {
        _api.Dispose();
        _tokens.Dispose();
    }

    [Fact]
    public async Task Health_is_reachable_without_a_token()
    {
        HttpResponseMessage response = await _api.CreateClient().GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData(MyMonthly)]
    [InlineData(SystemDaily)]
    public async Task Without_a_token_is_unauthorized(string path)
    {
        HttpResponseMessage response = await _api.CreateClient().GetAsync(path);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task MyMonthly_with_a_valid_token_is_ok()
    {
        HttpResponseMessage response = await _api.ClientWith(_tokens.Customer()).GetAsync(MyMonthly);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // Mehr Zustimmung des Kunden hilft hier nicht, also kein insufficient_scope.
    [Fact]
    public async Task SystemDaily_with_the_scope_but_without_the_admin_role_is_forbidden()
    {
        HttpResponseMessage response = await _api.ClientWith(_tokens.Customer()).GetAsync(SystemDaily);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.False(response.Headers.Contains("WWW-Authenticate"));
    }

    [Theory]
    [InlineData(MyMonthly, "openid profile")]
    [InlineData(MyMonthly, null)]
    [InlineData(MyMonthly, "openid analytics:readonly statements:read")]
    [InlineData(SystemDaily, "openid profile")]
    public async Task A_valid_token_without_the_scope_is_forbidden_and_names_the_scope(string path, string? scope)
    {
        string token = _tokens.Issue(new TokenSpec { Scope = scope, Roles = new[] { "bank-admin" } });

        HttpResponseMessage response = await _api.ClientWith(token).GetAsync(path);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(InsufficientScope, Assert.Single(response.Headers.GetValues("WWW-Authenticate")));
        Assert.Equal(0, response.Content.Headers.ContentLength ?? 0);
    }

    [Fact]
    public async Task The_scope_is_found_among_other_scopes()
    {
        string token = _tokens.Issue(new TokenSpec { Scope = "openid email analytics:read statements:read profile" });

        HttpResponseMessage response = await _api.ClientWith(token).GetAsync(MyMonthly);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task A_customer_sees_only_their_own_totals()
    {
        await _postgres.ResetAsync();
        await BookAsync("kunde-a", 100m);
        await BookAsync("kunde-a", 50m);
        await BookAsync("kunde-b", 999m);

        JsonElement[] ownTotals = await _api.ClientWith(_tokens.CustomerNamed("kunde-a"))
            .GetFromJsonAsync<JsonElement[]>(MyMonthly) ?? Array.Empty<JsonElement>();
        JsonElement[] strangerTotals = await _api.ClientWith(_tokens.CustomerNamed("kunde-c"))
            .GetFromJsonAsync<JsonElement[]>(MyMonthly) ?? Array.Empty<JsonElement>();

        JsonElement month = Assert.Single(ownTotals);
        Assert.Equal(150m, month.GetProperty("income").GetDecimal());
        Assert.Equal(2, month.GetProperty("transactions").GetInt32());
        Assert.Empty(strangerTotals);
    }

    [Theory]
    [InlineData("/v1/analytics/system/daily?to=2026-09-30")]
    [InlineData("/v1/analytics/system/daily?from=2026-09-01")]
    [InlineData("/v1/analytics/system/daily")]
    public async Task SystemDaily_without_from_or_to_is_a_bad_request(string path)
    {
        HttpResponseMessage response = await _api.ClientWith(_tokens.Customer("bank-admin")).GetAsync(path);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SystemDaily_with_the_admin_role_is_ok()
    {
        HttpResponseMessage response = await _api.ClientWith(_tokens.Customer("bank-admin")).GetAsync(SystemDaily);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task SystemDaily_with_from_after_to_is_a_bad_request()
    {
        HttpResponseMessage response = await _api.ClientWith(_tokens.Customer("bank-admin"))
            .GetAsync("/v1/analytics/system/daily?from=2026-09-30&to=2026-09-01");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task MyMonthly_with_a_year_out_of_range_is_a_bad_request()
    {
        HttpResponseMessage response = await _api.ClientWith(_tokens.Customer()).GetAsync("/v1/analytics/me/monthly?year=1999");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task BookAsync(string owner, decimal amount)
    {
        await using AnalyticsDbContext context = _postgres.CreateContext();
        await new LedgerProjection(new TotalsStore(context)).ApplyAsync(
            BookedTransaction.Of(Guid.NewGuid(), owner, TransactionKind.Deposit, amount, Currency.Of("CHF"),
                DateTimeOffset.Parse("2026-09-17T09:30:00Z")),
            CancellationToken.None);
    }

    public static TheoryData<string, TokenSpec> RejectedTokens()
    {
        return new TheoryData<string, TokenSpec>
        {
            { "ID Token statt Access Token", new TokenSpec { TokenType = "ID" } },
            { "falsche Audience", new TokenSpec { Audiences = new[] { "m321-accounts-api" } } },
            { "falscher Aussteller", new TokenSpec { Issuer = "https://evil.test/realms/bank" } },
            { "abgelaufen", new TokenSpec { Expired = true } },
            { "ohne sub", new TokenSpec { Subject = null } },
            { "ohne iat", new TokenSpec { WithIssuedAt = false } },
            { "unbekannter Schlüssel", new TokenSpec { Untrusted = true } },
            { "RS256 statt ES256", new TokenSpec { Algorithm = SecurityAlgorithms.RsaSha256 } }
        };
    }

    [Theory]
    [MemberData(nameof(RejectedTokens))]
    public async Task An_invalid_token_is_unauthorized(string reason, TokenSpec spec)
    {
        HttpResponseMessage response = await _api.ClientWith(_tokens.Issue(spec)).GetAsync(MyMonthly);

        Assert.True(response.StatusCode == HttpStatusCode.Unauthorized, $"Erwartet 401 bei: {reason}");
    }
}
