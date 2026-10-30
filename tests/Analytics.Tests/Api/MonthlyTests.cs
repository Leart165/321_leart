using Analytics.Tests.Bank;
using Analytics.Tests.Persistence;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Analytics.Tests.Api;

// Die Monatssummen rechnet der Dienst aus den Buchungen eines ganzen Jahres bei der Bank:
// from = 1. Januar, to = 1. Januar des Folgejahres, Monate in UTC.
public sealed class MonthlyTests : IClassFixture<PostgresFixture>, IAsyncLifetime, IDisposable
{
    private readonly PostgresFixture _postgres;
    private readonly TestTokenIssuer _tokens = new TestTokenIssuer();
    private readonly AnalyticsApiFactory _api;

    public MonthlyTests(PostgresFixture postgres)
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

    [Theory]
    [InlineData("/v1/analytics/me/monthly?year=2026")]
    [InlineData("/v2/analytics/me/monthly?year=2026")]
    public async Task A_year_is_asked_as_one_half_open_range_and_summed_per_utc_month(string path)
    {
        FakeStatementsApi bank = _api.Bank;
        bank.Book(TestTokenIssuer.Subject, "Deposit", 999m, DateTimeOffset.Parse("2025-12-31T23:59:59Z"));
        bank.Book(TestTokenIssuer.Subject, "Deposit", 100m, DateTimeOffset.Parse("2026-01-01T00:00:00Z"));
        bank.Book(TestTokenIssuer.Subject, "TransferIn", 50m, DateTimeOffset.Parse("2026-01-15T12:00:00Z"));
        bank.Book(TestTokenIssuer.Subject, "Withdrawal", 30m, DateTimeOffset.Parse("2026-12-31T23:30:00Z"));
        bank.Book(TestTokenIssuer.Subject, "Deposit", 999m, DateTimeOffset.Parse("2027-01-01T00:00:00Z"));

        HttpResponseMessage response = await _api.ClientWith(_tokens.Customer()).GetAsync(path);
        JsonElement[] months = await response.Content.ReadFromJsonAsync<JsonElement[]>() ?? Array.Empty<JsonElement>();

        BankCall call = Assert.Single(bank.Calls);
        Assert.Equal(DateTimeOffset.Parse("2026-01-01T00:00:00Z"), DateTimeOffset.Parse(call.From!));
        Assert.Equal(DateTimeOffset.Parse("2027-01-01T00:00:00Z"), DateTimeOffset.Parse(call.To!));

        Assert.Collection(
            months,
            january =>
            {
                Assert.Equal(1, january.GetProperty("month").GetInt32());
                Assert.Equal(150m, january.GetProperty("income").GetDecimal());
                Assert.Equal(2, january.GetProperty("transactions").GetInt32());
            },
            december =>
            {
                Assert.Equal(12, december.GetProperty("month").GetInt32());
                Assert.Equal(30m, december.GetProperty("expenses").GetDecimal());
                Assert.Equal(-30m, december.GetProperty("net").GetDecimal());
            });
    }

    [Fact]
    public async Task Without_consent_for_the_bank_the_totals_are_forbidden_and_name_statements_read()
    {
        string token = _tokens.Issue(new TokenSpec { Scope = "openid analytics:read" });

        HttpResponseMessage response = await _api.ClientWith(token).GetAsync("/v1/analytics/me/monthly?year=2026");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("scope=\"statements:read\"", Assert.Single(response.Headers.GetValues("WWW-Authenticate")));
        Assert.Empty(_api.Bank.Calls);
    }
}
