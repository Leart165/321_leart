using Analytics.Application.Reports;
using Analytics.Domain.Ledger;
using Analytics.Domain.Reports;
using Analytics.Infrastructure;
using Analytics.Infrastructure.Messaging;
using Analytics.Infrastructure.Outbox;
using Analytics.Infrastructure.Persistence;
using Analytics.Tests.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using Xunit;

namespace Analytics.Tests.Reports;

// Beantragen und Erzeugen gegen eine echte Datenbank: Bericht und Outbox in einer Transaktion,
// Erzeugen idempotent, auch wenn zwei Instanzen denselben Antrag gleichzeitig bearbeiten.
public sealed class ReportServiceTests : IClassFixture<PostgresFixture>, IAsyncLifetime, IDisposable
{
    private const string Owner = "kunde-1";

    private readonly PostgresFixture _postgres;
    private readonly ServiceProvider _services;

    public ReportServiceTests(PostgresFixture postgres)
    {
        _postgres = postgres;
        _services = Services(postgres.ConnectionString);
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
        _services.Dispose();
    }

    [Fact]
    public async Task A_request_stores_the_report_and_the_event_in_the_outbox()
    {
        MonthlyReport report = await RequestAsync(2026, 9, "corr-1");

        await using AnalyticsDbContext context = _postgres.CreateContext();
        MonthlyReport stored = await context.MonthlyReports.SingleAsync();
        Assert.Equal(report.Id, stored.Id);
        Assert.Equal(ReportStatus.Requested, stored.Status);
        Assert.Equal(ReportPeriod.Of(2026, 9), stored.Period);

        OutboxMessage message = await context.Outbox.SingleAsync();
        Assert.Equal("report.requested", message.RoutingKey);
        Assert.Equal("ReportRequested", message.Type);
        Assert.Equal("corr-1", message.CorrelationId);
        Assert.Null(message.PublishedAt);

        using JsonDocument payload = JsonDocument.Parse(message.Payload);
        Assert.Equal(report.Id, payload.RootElement.GetProperty("reportId").GetGuid());
        Assert.Equal(9, payload.RootElement.GetProperty("month").GetInt32());
    }

    [Fact]
    public async Task Generating_creates_the_pdf_from_the_bookings_of_the_month_and_marks_it_ready()
    {
        await BookAsync(Owner, 100m, new DateTimeOffset(2026, 9, 3, 10, 0, 0, TimeSpan.Zero));
        await BookAsync(Owner, 50m, new DateTimeOffset(2026, 9, 30, 23, 59, 0, TimeSpan.Zero));
        await BookAsync(Owner, 999m, new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero));
        await BookAsync("jemand-anders", 999m, new DateTimeOffset(2026, 9, 3, 10, 0, 0, TimeSpan.Zero));
        MonthlyReport report = await RequestAsync(2026, 9);

        ReportGeneration result = await GenerateAsync(report.Id);

        Assert.Equal(ReportGeneration.Generated, result);
        ReportDocument document = await WithServiceAsync(service => service.GetDocumentAsync(report.Id, Owner, CancellationToken.None));
        Assert.Equal("application/pdf", document.ContentType);
        Assert.Equal("buchungen-2026-09.pdf", document.FileName);
        Assert.StartsWith("%PDF-1.4", System.Text.Encoding.Latin1.GetString(document.Content));

        MonthlyReport ready = await WithServiceAsync(service => service.GetOwnedAsync(report.Id, Owner, CancellationToken.None));
        Assert.Equal(ReportStatus.Ready, ready.Status);
        Assert.Equal(2, ready.BookingCount);
    }

    [Fact]
    public async Task A_second_delivery_of_the_same_request_changes_nothing()
    {
        MonthlyReport report = await RequestAsync(2026, 9);

        Assert.Equal(ReportGeneration.Generated, await GenerateAsync(report.Id));
        Assert.Equal(ReportGeneration.AlreadyCompleted, await GenerateAsync(report.Id));

        await using AnalyticsDbContext context = _postgres.CreateContext();
        Assert.Equal(1, await context.ReportDocuments.CountAsync());
    }

    [Fact]
    public async Task When_two_instances_generate_at_the_same_time_only_one_wins()
    {
        MonthlyReport report = await RequestAsync(2026, 9);

        using IServiceScope first = _services.CreateScope();
        using IServiceScope second = _services.CreateScope();
        ReportService one = first.ServiceProvider.GetRequiredService<ReportService>();
        ReportService two = second.ServiceProvider.GetRequiredService<ReportService>();

        ReportGeneration[] results = await Task.WhenAll(
            one.GenerateAsync(report.Id, CancellationToken.None),
            two.GenerateAsync(report.Id, CancellationToken.None));

        Assert.Single(results, result => result == ReportGeneration.Generated);
        Assert.Single(results, result => result == ReportGeneration.AlreadyCompleted);
        await using AnalyticsDbContext context = _postgres.CreateContext();
        Assert.Equal(1, await context.ReportDocuments.CountAsync());
    }

    [Fact]
    public async Task An_unknown_report_is_reported_as_unknown()
    {
        Assert.Equal(ReportGeneration.Unknown, await GenerateAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task A_foreign_report_looks_like_an_unknown_one()
    {
        MonthlyReport report = await RequestAsync(2026, 9);

        await Assert.ThrowsAsync<ReportNotFoundException>(() =>
            WithServiceAsync(service => service.GetOwnedAsync(report.Id, "jemand-anders", CancellationToken.None)));
        await Assert.ThrowsAsync<ReportNotFoundException>(() =>
            WithServiceAsync(service => service.GetDocumentAsync(report.Id, "jemand-anders", CancellationToken.None)));
    }

    [Fact]
    public async Task There_is_no_document_before_the_report_is_ready()
    {
        MonthlyReport report = await RequestAsync(2026, 9);

        ReportNotReadyException exception = await Assert.ThrowsAsync<ReportNotReadyException>(() =>
            WithServiceAsync(service => service.GetDocumentAsync(report.Id, Owner, CancellationToken.None)));
        Assert.Equal(ReportStatus.Requested, exception.Status);
    }

    [Fact]
    public async Task The_list_shows_only_own_reports_newest_first()
    {
        MonthlyReport older = await RequestAsync(2026, 8);
        MonthlyReport newer = await RequestAsync(2026, 9);
        await WithServiceAsync(service => service.RequestAsync("jemand-anders", 2026, 9, "corr", CancellationToken.None));

        IReadOnlyList<MonthlyReport> reports = await WithServiceAsync(service => service.ListOwnedAsync(Owner, CancellationToken.None));

        Assert.Equal(new[] { newer.Id, older.Id }, reports.Select(report => report.Id));
    }

    public static ServiceProvider Services(string connectionString)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:" + DependencyInjection.ConnectionStringName] = connectionString
            })
            .Build();

        ServiceCollection services = new ServiceCollection();
        services.AddLogging();
        services.AddAnalyticsCore(configuration);

        // Was AddReportProcessing sonst registriert, ohne die Hintergrunddienste: die startet
        // ein Test selbst, mit dem Broker der Testklasse.
        services.AddSingleton(AsyncApiSchemas.FromEmbeddedContracts());
        services.AddScoped<ReportRequestedHandler>();
        return services.BuildServiceProvider();
    }

    private Task<MonthlyReport> RequestAsync(int year, int month, string correlationId = "corr")
    {
        return WithServiceAsync(service => service.RequestAsync(Owner, year, month, correlationId, CancellationToken.None));
    }

    private Task<ReportGeneration> GenerateAsync(Guid reportId)
    {
        return WithServiceAsync(service => service.GenerateAsync(reportId, CancellationToken.None));
    }

    private async Task<T> WithServiceAsync<T>(Func<ReportService, Task<T>> action)
    {
        using IServiceScope scope = _services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<ReportService>());
    }

    private async Task BookAsync(string owner, decimal amount, DateTimeOffset bookedAt)
    {
        await using AnalyticsDbContext context = _postgres.CreateContext();
        await new Analytics.Application.Ledger.LedgerProjection(new TotalsStore(context)).ApplyAsync(
            BookedTransaction.Of(Guid.NewGuid(), owner, TransactionKind.Deposit, amount, Currency.Of("CHF"), bookedAt),
            CancellationToken.None);
    }
}
