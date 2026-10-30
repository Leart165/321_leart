using Analytics.Tests.Contracts;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace Analytics.Tests.Observability;

// Der eigene Observability-Stack unter observability/: Grafana zeigt nur, was die eigenen
// Datenquellen liefern, und Alloy sammelt nur die eigenen Container.
public sealed class ObservabilityConfigTests
{
    private static string Read(params string[] path)
    {
        return File.ReadAllText(Path.Combine(new[] { ContractFile.RepositoryRoot(), "observability" }.Concat(path).ToArray()));
    }

    // Wie bei der Bank drei Dashboards: Logs, Metriken, Traces.
    [Fact]
    public void There_are_the_three_dashboards_of_the_bank_for_the_analytics_firm()
    {
        string folder = Path.Combine(ContractFile.RepositoryRoot(), "observability", "grafana", "dashboards");

        List<string> uids = Directory.GetFiles(folder, "*.json")
            .Select(file =>
            {
                using JsonDocument dashboard = JsonDocument.Parse(File.ReadAllText(file));
                return dashboard.RootElement.GetProperty("uid").GetString()!;
            })
            .Order()
            .ToList();

        Assert.Equal(new[] { "analytics-logs", "analytics-metrics", "analytics-traces" }, uids);
    }

    [Theory]
    [InlineData("analytics-logs.json")]
    [InlineData("analytics-metrics.json")]
    [InlineData("analytics-traces.json")]
    public void Every_panel_uses_a_provisioned_data_source_and_no_metric_of_the_bank(string file)
    {
        string datasources = Read("grafana", "provisioning", "datasources", "datasources.yaml");
        HashSet<string> provisioned = Regex.Matches(datasources, @"^\s+uid:\s*(\S+)", RegexOptions.Multiline)
            .Select(match => match.Groups[1].Value)
            .ToHashSet();
        string json = Read("grafana", "dashboards", file);

        using JsonDocument dashboard = JsonDocument.Parse(json);
        List<string> used = Panels(dashboard.RootElement.GetProperty("panels"))
            .Where(panel => panel.TryGetProperty("datasource", out JsonElement source) && source.ValueKind == JsonValueKind.Object)
            .Select(panel => panel.GetProperty("datasource").GetProperty("uid").GetString()!)
            .Distinct()
            .ToList();

        Assert.NotEmpty(used);
        Assert.All(used, uid => Assert.Contains(uid, provisioned));
        Assert.DoesNotContain("accounts-api", json, StringComparison.Ordinal);
        Assert.DoesNotContain("bank_outbox", json, StringComparison.Ordinal);
        Assert.DoesNotContain("bank_circuit", json, StringComparison.Ordinal);
    }

    private static IEnumerable<JsonElement> Panels(JsonElement panels)
    {
        foreach (JsonElement panel in panels.EnumerateArray())
        {
            yield return panel;
            if (panel.TryGetProperty("panels", out JsonElement nested))
            {
                foreach (JsonElement inner in Panels(nested))
                {
                    yield return inner;
                }
            }
        }
    }

    [Fact]
    public void The_data_sources_point_only_to_the_own_stack()
    {
        string datasources = Read("grafana", "provisioning", "datasources", "datasources.yaml");

        List<string> urls = Regex.Matches(datasources, @"url:\s*(http://\S+)")
            .Select(match => match.Groups[1].Value)
            .ToList();

        Assert.Equal(3, urls.Count);
        Assert.All(urls, url => Assert.StartsWith("http://analytics-", url, StringComparison.Ordinal));
    }

    [Fact]
    public void Alloy_keeps_only_the_containers_of_the_analytics_firm_and_writes_only_to_its_own_stack()
    {
        string alloy = Read("alloy", "config.alloy");

        Assert.Contains("regex         = \"analytics-.*|analyticsdb\"\n    action        = \"keep\"", alloy, StringComparison.Ordinal);
        List<string> targets = Regex.Matches(alloy, @"(?:url|endpoint)\s*=\s*""(?:http://)?([a-z0-9.-]+):\d+")
            .Select(match => match.Groups[1].Value)
            .Where(host => host != "0.0.0.0")
            .ToList();
        Assert.NotEmpty(targets);
        Assert.All(targets, host => Assert.StartsWith("analytics-", host, StringComparison.Ordinal));
    }

    [Fact]
    public void The_service_sends_traces_and_metrics_to_the_own_alloy()
    {
        string compose = File.ReadAllText(Path.Combine(ContractFile.RepositoryRoot(), "docker-compose.yml"));

        Assert.Contains("OTEL_EXPORTER_OTLP_ENDPOINT: http://analytics-alloy:4317", compose, StringComparison.Ordinal);
    }
}
