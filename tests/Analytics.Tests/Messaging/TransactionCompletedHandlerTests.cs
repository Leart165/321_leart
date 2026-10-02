using Analytics.Application.Ledger;
using Analytics.Infrastructure.Messaging;
using Analytics.Infrastructure.Persistence;
using Analytics.Tests.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text;
using Xunit;

namespace Analytics.Tests.Messaging;

// Was mit einer Nachricht geschieht, gegen eine echte Datenbank. Der Konsument setzt das
// Ergebnis nur noch in Ack oder Nack um.
public sealed class TransactionCompletedHandlerTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private const string Type = "TransactionCompleted";
    private const string PartnerType = "PartnerTransactionCompleted";

    private readonly PostgresFixture _postgres;

    public TransactionCompletedHandlerTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    public Task InitializeAsync()
    {
        return _postgres.ResetAsync();
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    [Fact]
    public async Task A_valid_booking_is_counted()
    {
        string outcome = await HandleAsync(AsyncApiSchemasTests.ValidBooking, Type);

        Assert.Equal(ConsumeOutcome.Processed, outcome);
        Assert.Equal(100.50m, (await SingleMonthlyAsync()).Income);
    }

    [Fact]
    public async Task The_same_booking_delivered_twice_is_counted_once()
    {
        string first = await HandleAsync(AsyncApiSchemasTests.ValidBooking, Type);
        string second = await HandleAsync(AsyncApiSchemasTests.ValidBooking, Type, redelivered: true);

        Assert.Equal(ConsumeOutcome.Processed, first);
        Assert.Equal(ConsumeOutcome.Duplicate, second);
        Assert.Equal(1, (await SingleMonthlyAsync()).Transactions);
    }

    [Fact]
    public async Task A_booking_that_breaks_the_contract_goes_to_the_dead_letter_queue_untouched()
    {
        string broken = AsyncApiSchemasTests.ValidBooking.Replace("\"Deposit\"", "\"Bonus\"", StringComparison.Ordinal);

        string outcome = await HandleAsync(broken, Type);

        Assert.Equal(ConsumeOutcome.DeadLettered, outcome);
        await using AnalyticsDbContext context = _postgres.CreateContext();
        Assert.False(await context.OwnerMonthly.AnyAsync());
    }

    [Fact]
    public async Task Without_a_type_header_the_booking_is_still_read()
    {
        Assert.Equal(ConsumeOutcome.Processed, await HandleAsync(AsyncApiSchemasTests.ValidBooking, messageType: null));
    }

    [Fact]
    public async Task Garbage_goes_to_the_dead_letter_queue()
    {
        Assert.Equal(ConsumeOutcome.DeadLettered, await HandleAsync("kein json", messageType: null));
    }

    [Fact]
    public async Task An_unreachable_database_is_retried_and_never_dead_lettered()
    {
        DbContextOptionsBuilder<AnalyticsDbContext> options = new DbContextOptionsBuilder<AnalyticsDbContext>();
        options.UseNpgsql("Host=127.0.0.1;Port=1;Database=analytics;Username=analytics;Password=analytics;Timeout=1");

        await using AnalyticsDbContext unreachable = new AnalyticsDbContext(options.Options);

        string outcome = await HandleAsync(unreachable, AsyncApiSchemasTests.ValidBooking, BookingFormat.Internal, Type, redelivered: true);

        Assert.Equal(ConsumeOutcome.Retried, outcome);
    }

    // Das Partner-Ereignis hat kein accountId. Die alte Lesart hätte es in die Dead-Letter-Queue geschickt.
    [Fact]
    public async Task A_partner_booking_without_an_account_is_counted()
    {
        string outcome = await HandleAsync(AsyncApiSchemasTests.ValidPartnerBooking, BookingFormat.Partner, PartnerType);

        Assert.Equal(ConsumeOutcome.Processed, outcome);
        Assert.Equal(100.50m, (await SingleMonthlyAsync()).Income);
    }

    // Expand: dieselbe Buchung kommt über analytics.ledger und analytics.partner, in beliebiger
    // Reihenfolge, und zählt genau einmal.
    [Theory]
    [InlineData(BookingFormat.Internal, BookingFormat.Partner)]
    [InlineData(BookingFormat.Partner, BookingFormat.Internal)]
    public async Task The_same_transaction_over_both_queues_is_counted_once(BookingFormat first, BookingFormat second)
    {
        string firstOutcome = await HandleAsync(Booking(first), first, TypeOf(first));
        string secondOutcome = await HandleAsync(Booking(second), second, TypeOf(second));

        Assert.Equal(ConsumeOutcome.Processed, firstOutcome);
        Assert.Equal(ConsumeOutcome.Duplicate, secondOutcome);

        OwnerMonthly monthly = await SingleMonthlyAsync();
        Assert.Equal(100.50m, monthly.Income);
        Assert.Equal(1, monthly.Transactions);
    }

    [Fact]
    public async Task A_partner_booking_that_breaks_the_contract_goes_to_the_dead_letter_queue()
    {
        string withAccount = AsyncApiSchemasTests.ValidPartnerBooking.Replace(
            "\"ownerId\": \"kunde-1\",", "\"ownerId\": \"kunde-1\", \"description\": \"Lohn\",", StringComparison.Ordinal);

        Assert.Equal(ConsumeOutcome.DeadLettered, await HandleAsync(withAccount, BookingFormat.Partner, PartnerType));
    }

    private static string Booking(BookingFormat format)
    {
        return format == BookingFormat.Partner ? AsyncApiSchemasTests.ValidPartnerBooking : AsyncApiSchemasTests.ValidBooking;
    }

    private static string TypeOf(BookingFormat format)
    {
        return format == BookingFormat.Partner ? PartnerType : Type;
    }

    private Task<string> HandleAsync(string payload, string? messageType, bool redelivered = false)
    {
        return HandleAsync(payload, BookingFormat.Internal, messageType, redelivered);
    }

    private async Task<string> HandleAsync(string payload, BookingFormat format, string? messageType, bool redelivered = false)
    {
        await using AnalyticsDbContext context = _postgres.CreateContext();
        return await HandleAsync(context, payload, format, messageType, redelivered);
    }

    private static Task<string> HandleAsync(
        AnalyticsDbContext context, string payload, BookingFormat format, string? messageType, bool redelivered)
    {
        TransactionCompletedHandler handler = new TransactionCompletedHandler(
            new LedgerProjection(new TotalsStore(context)),
            AsyncApiSchemas.FromEmbeddedContracts(),
            NullLogger<TransactionCompletedHandler>.Instance);

        return handler.HandleAsync(Encoding.UTF8.GetBytes(payload), format, messageType, redelivered, CancellationToken.None);
    }

    private async Task<OwnerMonthly> SingleMonthlyAsync()
    {
        await using AnalyticsDbContext context = _postgres.CreateContext();
        return await context.OwnerMonthly.SingleAsync();
    }
}
