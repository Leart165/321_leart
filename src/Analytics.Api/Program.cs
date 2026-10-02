using Analytics.Api.Authentication;
using Analytics.Api.Frontend;
using Analytics.Api.Hosting;
using Analytics.Api.Middleware;
using Analytics.Api.Observability;
using Analytics.Api.OpenApi;
using Analytics.Infrastructure;
using Analytics.Infrastructure.Observability;
using Analytics.Infrastructure.Persistence;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Metriken und Traces an den Sammler, sofern OTEL_EXPORTER_OTLP_ENDPOINT gesetzt ist.
builder.AddAnalyticsObservability("analytics-api");
builder.Services.AddServerTracing();

builder.Services.AddAnalyticsCore(builder.Configuration);
builder.Services.AddDatabaseMigration();
builder.Services.AddLedgerConsumer();
builder.Services.AddReportProcessing();

builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddScopePolicies();
builder.Services.AddFrontend(builder.Configuration);

builder.Services.AddControllers(options =>
{
    options.SuppressAsyncSuffixInActionNames = false;
});
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<DatabaseUnavailableHandler>();

builder.Services.AddVersionedSwagger();

// Bereitschaft auf dem internen Port und Abfliessen beim Stoppen; Port und Dauer setzt das Dockerfile.
builder.Services.AddReadiness(builder.Configuration);

builder.Services.AddHealthChecks()
    .AddDbContextCheck<AnalyticsDbContext>("database");

WebApplication app = builder.Build();

app.UseExceptionHandler();
app.UseCorrelationId();
app.UseFrontend();
app.UseVersionedSwagger();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapFrontend();
app.MapReadiness();

app.Run();

public partial class Program
{
}
