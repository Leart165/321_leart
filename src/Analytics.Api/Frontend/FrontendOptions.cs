namespace Analytics.Api.Frontend;

public sealed class FrontendOptions
{
    public const string SectionName = "Frontend";

    // Öffentlicher Client der Analytics-Firma im Keycloak der Bank ("Sign in with Bank"),
    // Authorization Code mit PKCE, Standard-Scope analytics:read, siehe contracts/auth/scopes.md.
    public string ClientId { get; set; } = "analytics-web";
}
