using Analytics.Api.OpenApi;
using Analytics.Tests.Api;
using Analytics.Tests.Persistence;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace Analytics.Tests.Contracts;

// Vergleicht, was die laufende API anbietet (Swashbuckle aus den Controllern), mit dem, was ihr
// Kontrakt verspricht. Je Version eine Spezifikation und ein Kontrakt.
public abstract class OpenApiContractTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private static readonly string[] Methods = { "get", "post", "put", "patch", "delete" };

    private readonly string _version;
    private readonly PostgresFixture _postgres;
    private readonly TestTokenIssuer _tokens = new TestTokenIssuer();
    private JsonDocument? _generated;
    private JsonDocument? _contract;

    protected OpenApiContractTests(string version, PostgresFixture postgres)
    {
        _version = version;
        _postgres = postgres;
    }

    private JsonElement GeneratedPaths
    {
        get { return _generated!.RootElement.GetProperty("paths"); }
    }

    private JsonElement ContractPaths
    {
        get { return _contract!.RootElement.GetProperty("paths"); }
    }

    public async Task InitializeAsync()
    {
        using AnalyticsApiFactory api = new AnalyticsApiFactory(_postgres.ConnectionString, _tokens);
        string json = await api.CreateClient().GetStringAsync($"/swagger/{_version}/swagger.json");
        _generated = JsonDocument.Parse(json);
        _contract = ContractFile.Load($"analytics/openapi.{_version}.yaml");
    }

    public Task DisposeAsync()
    {
        _generated?.Dispose();
        _contract?.Dispose();
        _tokens.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public void Every_path_in_the_contract_exists_in_the_api()
    {
        List<string> missing = ContractPaths.EnumerateObject()
            .Select(path => path.Name)
            .Where(path => !GeneratedPaths.TryGetProperty(path, out _))
            .ToList();

        Assert.True(missing.Count == 0, "Im Kontrakt, aber nicht in der API: " + string.Join(", ", missing));
    }

    [Fact]
    public void Every_path_in_the_api_is_in_the_contract()
    {
        List<string> undocumented = GeneratedPaths.EnumerateObject()
            .Select(path => path.Name)
            .Where(path => !ContractPaths.TryGetProperty(path, out _))
            .ToList();

        Assert.True(undocumented.Count == 0, "In der API, aber nicht im Kontrakt: " + string.Join(", ", undocumented));
    }

    [Fact]
    public void Every_operation_offers_the_methods_and_status_codes_of_the_contract()
    {
        List<string> differences = new List<string>();

        foreach (JsonProperty path in ContractPaths.EnumerateObject())
        {
            if (!GeneratedPaths.TryGetProperty(path.Name, out JsonElement generated))
            {
                continue;
            }

            foreach (string method in Methods)
            {
                bool inContract = path.Value.TryGetProperty(method, out JsonElement contractOperation);
                bool inApi = generated.TryGetProperty(method, out JsonElement apiOperation);

                if (inContract != inApi)
                {
                    differences.Add($"{method.ToUpperInvariant()} {path.Name}: Kontrakt {inContract}, API {inApi}");
                    continue;
                }

                if (!inContract)
                {
                    continue;
                }

                // Seit der Scope-Prüfung kann jede Operation mit Scope 403 liefern. Version 1 ist
                // veröffentlicht und nennt das bei me/monthly noch nicht; wie der Kontrakt es
                // beschreibt, klärt Leart mit Tim. Bis dahin ist genau diese Abweichung erlaubt.
                IEnumerable<string> promised = ResponseCodes(contractOperation);
                if (ScopeContract.ScopesFor(method, path.Name).Count > 0)
                {
                    promised = promised.Append("403");
                }

                string expected = string.Join(",", promised.Distinct().Order());
                string actual = StatusCodes(apiOperation);
                if (expected != actual)
                {
                    differences.Add($"{method.ToUpperInvariant()} {path.Name}: Kontrakt {expected}, API {actual}");
                }
            }
        }

        Assert.True(differences.Count == 0, string.Join(Environment.NewLine, differences));
    }

    // Welcher Scope wofür nötig ist, gehört zur Zusage: ein Client fragt beim Kunden genau diesen
    // an. Verlangte der Code einen anderen, bekäme der Client trotz Zustimmung 403.
    [Fact]
    public void Every_operation_requires_the_scope_from_scopes_md()
    {
        List<string> differences = new List<string>();

        foreach (JsonProperty path in GeneratedPaths.EnumerateObject())
        {
            foreach (JsonProperty operation in path.Value.EnumerateObject())
            {
                IReadOnlyList<string> promised = ScopeContract.ScopesFor(operation.Name, path.Name);
                IReadOnlyList<string> required = RequiredScopes(operation.Value);

                if (!promised.SequenceEqual(required))
                {
                    differences.Add(
                        $"{operation.Name.ToUpperInvariant()} {path.Name}: scopes.md [{string.Join(" ", promised)}], API [{string.Join(" ", required)}]");
                }
            }
        }

        Assert.True(differences.Count == 0, "Scopes weichen von contracts/auth/scopes.md ab: " + string.Join("; ", differences));
    }

    [Fact]
    public void The_security_scheme_is_the_keycloak_of_the_bank_with_exactly_the_scopes_in_use()
    {
        JsonElement scheme = _generated!.RootElement.GetProperty("components").GetProperty("securitySchemes")
            .GetProperty(VersionedSwagger.SecurityScheme);
        JsonElement flow = scheme.GetProperty("flows").GetProperty("authorizationCode");

        Assert.Equal("oauth2", scheme.GetProperty("type").GetString());
        Assert.Equal(TestTokenIssuer.Issuer + "/protocol/openid-connect/auth", flow.GetProperty("authorizationUrl").GetString());
        Assert.Equal(TestTokenIssuer.Issuer + "/protocol/openid-connect/token", flow.GetProperty("tokenUrl").GetString());

        List<string> offered = flow.GetProperty("scopes").EnumerateObject().Select(scope => scope.Name).Order().ToList();
        List<string> used = GeneratedPaths.EnumerateObject()
            .SelectMany(path => path.Value.EnumerateObject())
            .SelectMany(operation => RequiredScopes(operation.Value))
            .Distinct()
            .Order()
            .ToList();

        Assert.Equal(used, offered);
    }

    // Die Antworten 401 und 403 kommen von der Authentifizierung, ohne Körper.
    [Fact]
    public void Unauthorized_and_forbidden_have_no_body()
    {
        foreach (JsonProperty path in GeneratedPaths.EnumerateObject())
        {
            foreach (JsonProperty operation in path.Value.EnumerateObject())
            {
                foreach (JsonProperty response in operation.Value.GetProperty("responses").EnumerateObject())
                {
                    if (response.Name is "401" or "403")
                    {
                        Assert.False(response.Value.TryGetProperty("content", out _), $"{path.Name} {response.Name} hat einen Körper");
                    }
                }
            }
        }
    }

    private static IReadOnlyList<string> RequiredScopes(JsonElement operation)
    {
        if (!operation.TryGetProperty("security", out JsonElement security))
        {
            return Array.Empty<string>();
        }

        return security.EnumerateArray()
            .SelectMany(requirement => requirement.EnumerateObject())
            .SelectMany(scheme => scheme.Value.EnumerateArray())
            .Select(scope => scope.GetString()!)
            .Distinct()
            .Order()
            .ToList();
    }

    private static IEnumerable<string> ResponseCodes(JsonElement operation)
    {
        return operation.GetProperty("responses").EnumerateObject().Select(response => response.Name);
    }

    private static string StatusCodes(JsonElement operation)
    {
        return string.Join(",", ResponseCodes(operation).Order());
    }
}

// Liest die Scope-Tabelle aus contracts/auth/scopes.md: je Scope die Endpunkte, die ihn verlangen,
// geschrieben als `GET /v1/...`.
public static class ScopeContract
{
    private static readonly Lazy<IReadOnlyList<(string Scope, string Method, string Path)>> Rows =
        new Lazy<IReadOnlyList<(string, string, string)>>(Load);

    public static IReadOnlyList<string> ScopesFor(string method, string path)
    {
        return Rows.Value
            .Where(row => string.Equals(row.Method, method, StringComparison.OrdinalIgnoreCase) && row.Path == path)
            .Select(row => row.Scope)
            .Distinct()
            .Order()
            .ToList();
    }

    private static IReadOnlyList<(string, string, string)> Load()
    {
        string markdown = File.ReadAllText(Path.Combine(ContractFile.RepositoryRoot(), "contracts", "auth", "scopes.md"));
        List<(string, string, string)> rows = new List<(string, string, string)>();

        foreach (string line in markdown.Split('\n'))
        {
            Match scope = Regex.Match(line, @"^\|\s*`(?<scope>[a-z]+:[a-z]+)`\s*\|");
            if (!scope.Success)
            {
                continue;
            }

            foreach (Match endpoint in Regex.Matches(line, @"`(?<method>GET|POST|PUT|PATCH|DELETE) (?<path>/[^`]+)`"))
            {
                rows.Add((scope.Groups["scope"].Value, endpoint.Groups["method"].Value, endpoint.Groups["path"].Value));
            }
        }

        return rows;
    }
}

public sealed class OpenApiV1ContractTests : OpenApiContractTests
{
    public OpenApiV1ContractTests(PostgresFixture postgres)
        : base("v1", postgres)
    {
    }
}

public sealed class OpenApiV2ContractTests : OpenApiContractTests
{
    public OpenApiV2ContractTests(PostgresFixture postgres)
        : base("v2", postgres)
    {
    }
}
