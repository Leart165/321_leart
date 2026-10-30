using System.Security.Claims;

namespace Analytics.Api.Authentication;

public static class ClaimsPrincipalExtensions
{
    // Nach erfolgreicher Prüfung ist sub immer gesetzt, JwtAuthentication lehnt Token ohne ab.
    public static string OwnerId(this ClaimsPrincipal user)
    {
        return user.FindFirstValue(JwtAuthentication.SubjectClaim)
            ?? throw new InvalidOperationException("Das geprüfte Token hat keinen sub-Claim.");
    }

    // Ob der Kunde erlaubt hat, dass wir seine Buchungen bei der Bank lesen. Ohne das zeigt der
    // Dienst nichts, auch nicht, was er früher einmal gespeichert hat.
    public static bool HasConsentedToBankBookings(this ClaimsPrincipal user)
    {
        return ScopeHandler.HasScope(user, Scopes.StatementsRead);
    }
}
