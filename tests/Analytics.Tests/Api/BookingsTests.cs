using Analytics.Infrastructure.Persistence;
using Analytics.Tests.Bank;
using Analytics.Tests.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Analytics.Tests.Api;

// GET /v2/analytics/me/bookings: jede Buchung des Kunden, gelesen bei der Bank mit seinem Token
// und nur mit seiner Zustimmung. Ist die Bank weg, der gespeicherte Stand, und nur dann.
public sealed class BookingsTests : IClassFixture<PostgresFixture>, IAsyncLifetime, IDisposable
{
    private const string Bookings = "/v2/analytics/me/bookings";
    private const string September = Bookings + "?from=2026-09-01&to=2026-09-30";

    private readonly PostgresFixture _postgres;
    private readonly TestTokenIssuer _tokens = new TestTokenIssuer();
    private readonly AnalyticsApiFactory _api;

    public BookingsTests(PostgresFixture postgres)
    {
        _postgres = postgres;
        _api = new AnalyticsApiFactory(postgres.ConnectionString, _tokens);
    }

    private FakeStatementsApi Bank
    {
        get { return _api.Bank; }
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
    public async Task The_bookings_come_from_the_bank_newest_first_and_are_marked_live()
    {
        Bank.Book(TestTokenIssuer.Subject, "Deposit", 5400m, At("2026-09-25T08:00:00Z"));
        Bank.Book(TestTokenIssuer.Subject, "Withdrawal", 42.50m, At("2026-09-26T12:15:00Z"));

        HttpResponseMessage response = await _api.ClientWith(_tokens.Customer()).GetAsync(September);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("live", Assert.Single(response.Headers.GetValues("X-Data-Source")));
        JsonElement[] log = await ReadAsync(response);
        Assert.Equal(new[] { "Withdrawal", "Deposit" }, log.Select(entry => entry.GetProperty("kind").GetString()));
        Assert.Equal(42.50m, log[0].GetProperty("amount").GetDecimal());
    }

    [Fact]
    public async Task The_token_and_the_correlation_id_of_the_customer_go_to_the_bank()
    {
        string token = _tokens.Customer();
        HttpClient client = _api.ClientWith(token);
        client.DefaultRequestHeaders.Add("X-Correlation-Id", "bank-1");

        await client.GetAsync(September);

        BankCall call = Assert.Single(Bank.Calls);
        Assert.Equal("/v2/statements/bookings", call.Path);
        Assert.Equal("Bearer " + token, call.Authorization);
        Assert.Equal("bank-1", call.CorrelationId);
    }

    // Die Bank rechnet halboffen in UTC: der letzte Tag gehört dazu, also bis zum Beginn des nächsten.
    [Fact]
    public async Task The_bank_is_asked_for_whole_utc_days_with_time_zone()
    {
        Bank.Book(TestTokenIssuer.Subject, "Deposit", 1m, At("2026-08-31T23:59:59Z"));
        Bank.Book(TestTokenIssuer.Subject, "Deposit", 2m, At("2026-09-01T00:00:00Z"));
        Bank.Book(TestTokenIssuer.Subject, "Deposit", 3m, At("2026-09-30T23:30:00Z"));
        Bank.Book(TestTokenIssuer.Subject, "Deposit", 4m, At("2026-10-01T00:00:00Z"));

        JsonElement[] log = await ReadAsync(await _api.ClientWith(_tokens.Customer()).GetAsync(September));

        BankCall call = Assert.Single(Bank.Calls);
        Assert.Equal(At("2026-09-01T00:00:00Z"), DateTimeOffset.Parse(call.From!));
        Assert.Equal(At("2026-10-01T00:00:00Z"), DateTimeOffset.Parse(call.To!));
        Assert.EndsWith("+00:00", call.From);
        Assert.Equal(new[] { 3m, 2m }, log.Select(entry => entry.GetProperty("amount").GetDecimal()));
    }

    [Fact]
    public async Task Without_a_range_the_last_30_days_are_asked()
    {
        await _api.ClientWith(_tokens.Customer()).GetAsync(Bookings);

        DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);
        BankCall call = Assert.Single(Bank.Calls);
        Assert.Equal(DateOnly.FromDateTime(DateTimeOffset.Parse(call.From!).UtcDateTime), today.AddDays(-29));
        Assert.Equal(DateOnly.FromDateTime(DateTimeOffset.Parse(call.To!).UtcDateTime), today.AddDays(1));
    }

    [Fact]
    public async Task A_customer_sees_only_their_own_bookings()
    {
        Bank.Book(TestTokenIssuer.Subject, "Deposit", 1m, At("2026-09-10T10:00:00Z"));
        Bank.Book("jemand-anders", "Deposit", 2m, At("2026-09-10T10:00:00Z"));

        JsonElement booking = Assert.Single(await ReadAsync(await _api.ClientWith(_tokens.Customer()).GetAsync(September)));
        Assert.Equal(1m, booking.GetProperty("amount").GetDecimal());
    }

    [Fact]
    public async Task At_most_limit_bookings_are_listed()
    {
        for (int day = 1; day <= 5; day++)
        {
            Bank.Book(TestTokenIssuer.Subject, "Deposit", day, At($"2026-09-{day:00}T10:00:00Z"));
        }

        JsonElement[] log = await ReadAsync(await _api.ClientWith(_tokens.Customer()).GetAsync(September + "&limit=2"));

        Assert.Equal(new[] { 5m, 4m }, log.Select(entry => entry.GetProperty("amount").GetDecimal()));
    }

    [Theory]
    [InlineData("?from=2026-09-30&to=2026-09-01")]
    [InlineData("?from=2025-01-01&to=2026-09-30")]
    [InlineData("?limit=0")]
    [InlineData("?limit=1001")]
    public async Task An_invalid_range_or_limit_is_a_bad_request_and_the_bank_is_not_asked(string query)
    {
        HttpResponseMessage response = await _api.ClientWith(_tokens.Customer()).GetAsync(Bookings + query);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(Bank.Calls);
    }

    [Fact]
    public async Task What_the_bank_delivered_is_shown_as_stored_while_the_bank_is_down()
    {
        Bank.Book(TestTokenIssuer.Subject, "Deposit", 100m, At("2026-09-10T10:00:00Z"));
        await _api.ClientWith(_tokens.Customer()).GetAsync(September);
        Bank.BeUnreachable();

        HttpResponseMessage response = await _api.ClientWith(_tokens.Customer()).GetAsync(September);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("stored", Assert.Single(response.Headers.GetValues("X-Data-Source")));
        JsonElement booking = Assert.Single(await ReadAsync(response));
        Assert.Equal(100m, booking.GetProperty("amount").GetDecimal());
    }

    [Fact]
    public async Task The_same_booking_delivered_twice_is_stored_once()
    {
        Bank.Book(TestTokenIssuer.Subject, "Deposit", 100m, At("2026-09-10T10:00:00Z"));

        await _api.ClientWith(_tokens.Customer()).GetAsync(September);
        await _api.ClientWith(_tokens.Customer()).GetAsync(September);

        await using AnalyticsDbContext context = _postgres.CreateContext();
        Assert.Equal(1, await context.OwnerBookings.CountAsync());
    }

    [Fact]
    public async Task Without_anything_stored_a_bank_outage_is_503_with_retry_after()
    {
        Bank.BeUnreachable();

        HttpResponseMessage response = await _api.ClientWith(_tokens.Customer()).GetAsync(September);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.NotNull(response.Headers.RetryAfter?.Delta);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task A_bank_that_hangs_ends_in_503_after_the_time_limit()
    {
        Bank.Hang();

        HttpResponseMessage response = await _api.ClientWith(_tokens.Customer()).GetAsync(September);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task A_missing_route_at_the_entrance_of_the_bank_counts_as_an_outage()
    {
        Bank.AnswerNoRoute();

        HttpResponseMessage response = await _api.ClientWith(_tokens.Customer()).GetAsync(September);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task A_booking_that_breaks_the_contract_is_not_counted()
    {
        Bank.AnswerBrokenBooking();

        HttpResponseMessage response = await _api.ClientWith(_tokens.Customer()).GetAsync(September);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        await using AnalyticsDbContext context = _postgres.CreateContext();
        Assert.Equal(0, await context.OwnerBookings.CountAsync());
    }

    [Fact]
    public async Task A_short_network_glitch_is_retried()
    {
        Bank.Book(TestTokenIssuer.Subject, "Deposit", 7m, At("2026-09-10T10:00:00Z"));
        Bank.FailOnceThenAnswer();

        HttpResponseMessage response = await _api.ClientWith(_tokens.Customer()).GetAsync(September);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, Bank.Calls.Count);
    }

    // Lehnt die Bank ab, hat der Kunde die Zustimmung zurückgenommen: kein gespeicherter Stand
    // mehr, auch nicht bei einem späteren Ausfall.
    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task A_refusal_of_the_bank_is_403_for_statements_read_and_forgets_what_was_stored(HttpStatusCode refusal)
    {
        Bank.Book(TestTokenIssuer.Subject, "Deposit", 100m, At("2026-09-10T10:00:00Z"));
        await _api.ClientWith(_tokens.Customer()).GetAsync(September);
        Bank.AnswerStatus(refusal);

        HttpResponseMessage response = await _api.ClientWith(_tokens.Customer()).GetAsync(September);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("Bearer error=\"insufficient_scope\", scope=\"statements:read\"",
            Assert.Single(response.Headers.GetValues("WWW-Authenticate")));
        await using AnalyticsDbContext context = _postgres.CreateContext();
        Assert.Equal(0, await context.OwnerBookings.CountAsync());
    }

    [Fact]
    public async Task Without_consent_in_the_token_the_bank_is_not_asked_and_nothing_stored_is_shown()
    {
        Bank.Book(TestTokenIssuer.Subject, "Deposit", 100m, At("2026-09-10T10:00:00Z"));
        await _api.ClientWith(_tokens.Customer()).GetAsync(September);
        Bank.BeUnreachable();
        string withoutConsent = _tokens.Issue(new TokenSpec { Scope = "openid analytics:read" });

        HttpResponseMessage response = await _api.ClientWith(withoutConsent).GetAsync(September);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("scope=\"statements:read\"", Assert.Single(response.Headers.GetValues("WWW-Authenticate")));
        Assert.Single(Bank.Calls);
        await using AnalyticsDbContext context = _postgres.CreateContext();
        Assert.Equal(0, await context.OwnerBookings.CountAsync());
    }

    [Fact]
    public async Task Without_a_token_the_log_is_unauthorized()
    {
        HttpResponseMessage response = await _api.CreateClient().GetAsync(Bookings);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(Bank.Calls);
    }

    private static DateTimeOffset At(string value)
    {
        return DateTimeOffset.Parse(value);
    }

    private static async Task<JsonElement[]> ReadAsync(HttpResponseMessage response)
    {
        return await response.Content.ReadFromJsonAsync<JsonElement[]>() ?? Array.Empty<JsonElement>();
    }
}
