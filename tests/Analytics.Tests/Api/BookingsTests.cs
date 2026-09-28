using Analytics.Application.Ledger;
using Analytics.Domain.Ledger;
using Analytics.Infrastructure.Persistence;
using Analytics.Tests.Persistence;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Analytics.Tests.Api;

// GET /v2/analytics/me/bookings: jede einzelne Buchung des Kunden, nur seine, nur mit Scope.
public sealed class BookingsTests : IClassFixture<PostgresFixture>, IAsyncLifetime, IDisposable
{
    private const string Bookings = "/v2/analytics/me/bookings";

    private readonly PostgresFixture _postgres;
    private readonly TestTokenIssuer _tokens = new TestTokenIssuer();
    private readonly AnalyticsApiFactory _api;

    public BookingsTests(PostgresFixture postgres)
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
    public async Task Without_a_range_the_last_30_days_are_listed()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        await BookAsync(TestTokenIssuer.Subject, TransactionKind.Deposit, 100m, now.AddHours(-1));
        await BookAsync(TestTokenIssuer.Subject, TransactionKind.Withdrawal, 20m, now.AddDays(-10));
        await BookAsync(TestTokenIssuer.Subject, TransactionKind.Deposit, 999m, now.AddDays(-45));

        JsonElement[] log = await GetAsync(Bookings);

        Assert.Equal(new[] { "Deposit", "Withdrawal" }, log.Select(entry => entry.GetProperty("kind").GetString()));
        Assert.Equal(100m, log[0].GetProperty("amount").GetDecimal());
        Assert.Equal("CHF", log[0].GetProperty("currency").GetString());
        Assert.True(log[0].TryGetProperty("transactionId", out _));
        Assert.True(log[0].TryGetProperty("bookedAt", out _));
    }

    [Fact]
    public async Task A_customer_sees_only_their_own_bookings()
    {
        await BookAsync(TestTokenIssuer.Subject, TransactionKind.Deposit, 1m, DateTimeOffset.UtcNow);
        await BookAsync("jemand-anders", TransactionKind.Deposit, 2m, DateTimeOffset.UtcNow);

        JsonElement booking = Assert.Single(await GetAsync(Bookings));
        Assert.Equal(1m, booking.GetProperty("amount").GetDecimal());
    }

    [Theory]
    [InlineData("?from=2026-09-30&to=2026-09-01")]
    [InlineData("?from=2025-01-01&to=2026-09-30")]
    [InlineData("?limit=0")]
    [InlineData("?limit=1001")]
    public async Task An_invalid_range_or_limit_is_a_bad_request(string query)
    {
        HttpResponseMessage response = await _api.ClientWith(_tokens.Customer()).GetAsync(Bookings + query);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Without_the_scope_the_log_is_forbidden()
    {
        string token = _tokens.Issue(new TokenSpec { Scope = "openid profile" });

        HttpResponseMessage response = await _api.ClientWith(token).GetAsync(Bookings);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("insufficient_scope", Assert.Single(response.Headers.GetValues("WWW-Authenticate")));
    }

    [Fact]
    public async Task Without_a_token_the_log_is_unauthorized()
    {
        HttpResponseMessage response = await _api.CreateClient().GetAsync(Bookings);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private async Task<JsonElement[]> GetAsync(string path)
    {
        return await _api.ClientWith(_tokens.Customer()).GetFromJsonAsync<JsonElement[]>(path) ?? Array.Empty<JsonElement>();
    }

    private async Task BookAsync(string owner, TransactionKind kind, decimal amount, DateTimeOffset bookedAt)
    {
        await using AnalyticsDbContext context = _postgres.CreateContext();
        await new LedgerProjection(new TotalsStore(context)).ApplyAsync(
            BookedTransaction.Of(Guid.NewGuid(), owner, kind, amount, Currency.Of("CHF"), bookedAt),
            CancellationToken.None);
    }
}
