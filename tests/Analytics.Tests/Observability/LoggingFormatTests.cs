using Analytics.Tests.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;
using Xunit;

namespace Analytics.Tests.Observability;

// Nur JSON mit Scopes liest Alloy so, dass Korrelations-Id und Trace-Id in Loki suchbar sind.
// Geprüft wird, was der Host aus der Konfiguration bindet, nicht der Text der Datei.
public sealed class LoggingFormatTests
{
    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public void The_api_logs_json_with_scopes_in_utc(string environment)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            ContentRootPath = Path.Combine(ContractFile.RepositoryRoot(), "src", "Analytics.Api"),
            EnvironmentName = environment
        });

        using IHost host = builder.Build();

        ConsoleLoggerOptions console = host.Services.GetRequiredService<IOptions<ConsoleLoggerOptions>>().Value;
        JsonConsoleFormatterOptions json = host.Services.GetRequiredService<IOptions<JsonConsoleFormatterOptions>>().Value;

        Assert.Equal(ConsoleFormatterNames.Json, console.FormatterName);
        Assert.True(json.IncludeScopes, "Ohne Scopes fehlen Korrelations-Id und Trace-Id in der Zeile.");
        Assert.True(json.UseUtcTimestamp);
    }
}
