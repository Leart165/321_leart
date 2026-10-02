using Analytics.Api.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Analytics.Api.OpenApi;

// Eine Spezifikation je Version, weil jede Version ihr eigener Kontrakt ist: die Vertragstests
// vergleichen /swagger/v1 mit openapi.v1.yaml und /swagger/v2 mit openapi.v2.yaml.
public static class VersionedSwagger
{
    public static readonly string[] Versions = { "v1", "v2" };

    // Name des Sicherheitsschemas, wie in den Kontrakten der Bank.
    public const string SecurityScheme = "keycloak";

    private const string Title = "Analytics API";

    public static IServiceCollection AddVersionedSwagger(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            foreach (string version in Versions)
            {
                options.SwaggerDoc(version, new OpenApiInfo { Title = Title, Version = version });
            }

            options.DocInclusionPredicate(BelongsToDocument);
            options.OperationFilter<ScopeOperationFilter>();
            options.OperationFilter<BodilessAuthResponsesFilter>();
        });

        // Die Adressen des Keycloak stehen in der Konfiguration, nicht im Code.
        services.AddOptions<SwaggerGenOptions>()
            .Configure<IOptions<AuthSettings>>((options, auth) =>
                options.AddSecurityDefinition(SecurityScheme, KeycloakScheme(auth.Value.Issuer)));

        return services;
    }

    public static WebApplication UseVersionedSwagger(this WebApplication app)
    {
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            foreach (string version in Versions)
            {
                // Relativ zur Oberfläche unter /swagger/: hinter Traefik liegt sie unter
                // /analytics/swagger/, und ein absoluter Pfad zeigte am Präfix vorbei.
                options.SwaggerEndpoint($"{version}/swagger.json", $"{Title} {version}");
            }
        });

        return app;
    }

    // OAuth2 statt nur "Bearer": so sagt die Spezifikation, welcher Scope wofür nötig ist.
    // Authorization Code mit PKCE, wie ihn "Sign in with Bank" über den Client analytics-web nutzt.
    private static OpenApiSecurityScheme KeycloakScheme(string issuer)
    {
        string endpoints = issuer.TrimEnd('/') + "/protocol/openid-connect";

        return new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.OAuth2,
            Description = "Access Token des Keycloak der Bank nach contracts/auth/jwt.md. Welcher Scope "
                + "wofür nötig ist, steht an jeder Operation und in contracts/auth/scopes.md.",
            Flows = new OpenApiOAuthFlows
            {
                AuthorizationCode = new OpenApiOAuthFlow
                {
                    AuthorizationUrl = new Uri(endpoints + "/auth"),
                    TokenUrl = new Uri(endpoints + "/token"),
                    Scopes = Scopes.All.ToDictionary(scope => scope, scope => Scopes.Descriptions[scope], StringComparer.Ordinal)
                }
            }
        };
    }

    // Pfade ohne Versionspräfix, also /health, gehören in jedes Dokument.
    private static bool BelongsToDocument(string documentName, ApiDescription description)
    {
        string path = description.RelativePath ?? string.Empty;

        foreach (string version in Versions)
        {
            if (path.StartsWith(version + "/", StringComparison.Ordinal))
            {
                return version == documentName;
            }
        }

        return true;
    }

    // Nennt an jeder tokenpflichtigen Operation die Scopes aus ihrem [Authorize(Policy = ...)].
    // Ein [AllowAnonymous] hebt ein [Authorize] des Controllers auf, trägt es aber weiter in den
    // Metadaten; deshalb wird es zuerst geprüft.
    private sealed class ScopeOperationFilter : IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            IList<object> metadata = context.ApiDescription.ActionDescriptor.EndpointMetadata;

            bool allowsAnonymous = metadata.OfType<IAllowAnonymous>().Any();
            IAuthorizeData[] authorize = metadata.OfType<IAuthorizeData>().ToArray();
            if (allowsAnonymous || authorize.Length == 0)
            {
                return;
            }

            List<string> scopes = authorize
                .Select(data => data.Policy)
                .OfType<string>()
                .Where(policy => Scopes.All.Contains(policy, StringComparer.Ordinal))
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToList();

            operation.Security ??= new List<OpenApiSecurityRequirement>();
            operation.Security.Add(new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference(SecurityScheme, context.Document)] = scopes
            });
        }
    }

    // 401 und 403 kommen von der Authentifizierung, nicht vom Controller, und haben keinen Körper;
    // der Grund steht im Kopf WWW-Authenticate. Ohne diesen Filter versprächen sie ProblemDetails.
    private sealed class BodilessAuthResponsesFilter : IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            if (operation.Responses is null)
            {
                return;
            }

            foreach (string status in new[] { "401", "403" })
            {
                if (operation.Responses.TryGetValue(status, out IOpenApiResponse? response) && response is OpenApiResponse concrete)
                {
                    concrete.Content?.Clear();
                    concrete.Description = status == "401"
                        ? "Kein oder ungültiges Token, siehe WWW-Authenticate"
                        : "Scope oder Rolle fehlt; fehlt der Scope, nennt WWW-Authenticate ihn als insufficient_scope";
                }
            }
        }
    }
}
