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
}
