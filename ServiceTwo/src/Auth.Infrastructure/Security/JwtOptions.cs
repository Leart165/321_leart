namespace Auth.Infrastructure.Security;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "http://localhost:8082";

    public string Audience { get; set; } = "bank-api";

    public int AccessTokenLifetimeSeconds { get; set; } = 3600;

    public string SigningKeyPath { get; set; } = "keys/auth-signing-key.pem";
}
