using Analytics.Api.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;

namespace Analytics.Tests.Api;

// Stellt Token aus, wie der Keycloak der Bank es täte, aber mit Schlüsseln, die nur im Test existieren.
public sealed class TestTokenIssuer : IDisposable
{
    public const string Issuer = "https://issuer.test/realms/bank";
    public const string Audience = "m321-analytics-api";
    public const string Subject = "kunde-1";

    private readonly ECDsa _trusted = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    private readonly ECDsa _untrusted = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    private readonly RSA _rsa = RSA.Create(2048);

    private SecurityKey TrustedKey
    {
        get { return new ECDsaSecurityKey(_trusted) { KeyId = "trusted-es256" }; }
    }

    // Auch der RSA-Schlüssel gilt als vertrauenswürdig: so scheitert ein RS256-Token nur am
    // Algorithmus und nicht an einer unbekannten Signatur.
    private SecurityKey RsaKey
    {
        get { return new RsaSecurityKey(_rsa) { KeyId = "trusted-rs256" }; }
    }

    public void ConfigureApi(IWebHostBuilder host)
    {
        host.UseSetting("Auth:Issuer", Issuer);
        host.UseSetting("Auth:Audience", Audience);
        host.ConfigureServices(services =>
        {
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.Authority = null;
                options.ConfigurationManager = null;
                options.TokenValidationParameters.IssuerSigningKeys = new[] { TrustedKey, RsaKey };
            });
        });
    }

    public string Customer(params string[] roles)
    {
        return Issue(new TokenSpec { Roles = roles });
    }

    public string CustomerNamed(string subject, params string[] roles)
    {
        return Issue(new TokenSpec { Subject = subject, Roles = roles });
    }

    public string Issue(TokenSpec spec)
    {
        Dictionary<string, object> claims = new Dictionary<string, object>();

        if (spec.Subject is not null)
        {
            claims[JwtAuthentication.SubjectClaim] = spec.Subject;
        }

        if (spec.TokenType is not null)
        {
            claims[JwtAuthentication.TokenTypeClaim] = spec.TokenType;
        }

        if (spec.Scope is not null)
        {
            claims[Scopes.Claim] = spec.Scope;
        }

        if (spec.Roles.Count > 0)
        {
            claims[JwtAuthentication.RealmAccessClaim] = new Dictionary<string, object> { ["roles"] = spec.Roles.ToArray() };
        }

        claims["aud"] = spec.Audiences.ToArray();

        // Abgelaufen heisst hier: länger als die fünf Minuten Uhrentoleranz der Prüfung.
        DateTime now = DateTime.UtcNow;
        DateTime issued = spec.Expired ? now.AddMinutes(-30) : now.AddMinutes(-1);
        SecurityTokenDescriptor descriptor = new SecurityTokenDescriptor
        {
            Issuer = spec.Issuer,
            Claims = claims,
            NotBefore = issued,
            Expires = spec.Expired ? now.AddMinutes(-10) : now.AddMinutes(5),
            IssuedAt = spec.WithIssuedAt ? issued : null,
            SigningCredentials = spec.Algorithm switch
            {
                SecurityAlgorithms.RsaSha256 => new SigningCredentials(RsaKey, SecurityAlgorithms.RsaSha256),
                _ when spec.Untrusted => new SigningCredentials(
                    new ECDsaSecurityKey(_untrusted) { KeyId = "untrusted" }, SecurityAlgorithms.EcdsaSha256),
                _ => new SigningCredentials(TrustedKey, SecurityAlgorithms.EcdsaSha256)
            }
        };

        JsonWebTokenHandler handler = new JsonWebTokenHandler { SetDefaultTimesOnTokenCreation = false };
        return handler.CreateToken(descriptor);
    }

    public void Dispose()
    {
        _trusted.Dispose();
        _untrusted.Dispose();
        _rsa.Dispose();
    }
}

public sealed record TokenSpec
{
    public string Issuer { get; init; } = TestTokenIssuer.Issuer;

    public IReadOnlyList<string> Audiences { get; init; } = new[] { TestTokenIssuer.Audience };

    public string? Subject { get; init; } = TestTokenIssuer.Subject;

    public string? TokenType { get; init; } = JwtAuthentication.AccessTokenType;

    // Wie Keycloak ihn für analytics-web ausstellt: durch Leerzeichen getrennt.
    public string? Scope { get; init; } = "openid profile analytics:read";

    public IReadOnlyList<string> Roles { get; init; } = Array.Empty<string>();

    public bool WithIssuedAt { get; init; } = true;

    public bool Expired { get; init; }

    public bool Untrusted { get; init; }

    public string Algorithm { get; init; } = SecurityAlgorithms.EcdsaSha256;
}
