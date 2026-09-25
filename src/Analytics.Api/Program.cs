using Analytics.Api.Authentication;
using Analytics.Api.Middleware;
using Analytics.Infrastructure;
using Analytics.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddAnalyticsCore(builder.Configuration);
builder.Services.AddDatabaseMigration();
builder.Services.AddLedgerConsumer();

builder.Services.AddJwtAuthentication(builder.Configuration);

builder.Services.AddControllers(options =>
{
    options.SuppressAsyncSuffixInActionNames = false;
});
builder.Services.AddProblemDetails();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddHealthChecks()
    .AddDbContextCheck<AnalyticsDbContext>("database");

WebApplication app = builder.Build();

app.UseExceptionHandler();
app.UseCorrelationId();
app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program
{
}
