using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text.Json;

namespace Analytics.Api.Authentication;

public static class JwtAuthentication
{
    public const string SubjectClaim = "sub";
    public const string TokenTypeClaim = "typ";
    public const string RealmAccessClaim = "realm_access";
    public const string RolesClaim = "roles";
    public const string RoleClaimType = "role";

    public const string AccessTokenType = "Bearer";
    public const string AdminRole = "bank-admin";

    private const string IssuedAtClaim = "iat";

    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<AuthSettings>()
            .Bind(configuration.GetSection(AuthSettings.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<AuthSettings>>((options, settings) => Configure(options, settings.Value));

        services.AddAuthorization();

        return services;
    }

    private static void Configure(JwtBearerOptions options, AuthSettings settings)
    {
        options.Authority = settings.Issuer;
        options.MapInboundClaims = false;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = settings.Issuer,

            ValidateAudience = true,
            ValidAudience = settings.Audience,

            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            RequireSignedTokens = true,
            RequireExpirationTime = true,

            ValidAlgorithms = new[] { SecurityAlgorithms.EcdsaSha256 },

            NameClaimType = SubjectClaim,
            RoleClaimType = RoleClaimType
        };

        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = ValidateContractClaimsAndMapRoles
        };
    }

    private static Task ValidateContractClaimsAndMapRoles(TokenValidatedContext context)
    {
        ClaimsPrincipal? principal = context.Principal;

        string? subject = principal?.FindFirst(SubjectClaim)?.Value;
        if (string.IsNullOrWhiteSpace(subject))
        {
            context.Fail("Das Token hat keinen sub-Claim.");
            return Task.CompletedTask;
        }

        string? tokenType = principal?.FindFirst(TokenTypeClaim)?.Value;
        if (!string.Equals(tokenType, AccessTokenType, StringComparison.Ordinal))
        {
            context.Fail("Das Token ist kein Access Token.");
            return Task.CompletedTask;
        }

        if (principal?.FindFirst(IssuedAtClaim) is null)
        {
            context.Fail("Das Token hat keinen iat-Claim.");
            return Task.CompletedTask;
        }

        MapRealmRoles(context);

        return Task.CompletedTask;
    }

    private static void MapRealmRoles(TokenValidatedContext context)
    {
        if (context.Principal?.Identity is not ClaimsIdentity identity)
        {
            return;
        }

        Claim? realmAccess = context.Principal.FindFirst(RealmAccessClaim);
        if (realmAccess is null)
        {
            return;
        }

        using JsonDocument document = JsonDocument.Parse(realmAccess.Value);
        if (!document.RootElement.TryGetProperty(RolesClaim, out JsonElement roles)
            || roles.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (JsonElement role in roles.EnumerateArray())
        {
            string? roleName = role.GetString();
            if (!string.IsNullOrWhiteSpace(roleName))
            {
                identity.AddClaim(new Claim(RoleClaimType, roleName));
            }
        }
    }
}
