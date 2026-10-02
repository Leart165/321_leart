using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using System.Security.Claims;

namespace Analytics.Api.Authentication;

public sealed class ScopeRequirement : IAuthorizationRequirement
{
    public ScopeRequirement(string scope)
    {
        Scope = scope;
    }

    public string Scope { get; }
}

public sealed class ScopeHandler : AuthorizationHandler<ScopeRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, ScopeRequirement requirement)
    {
        if (HasScope(context.User, requirement.Scope))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }

    public static bool HasScope(ClaimsPrincipal user, string scope)
    {
        return user.FindAll(Scopes.Claim)
            .SelectMany(claim => claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Contains(scope, StringComparer.Ordinal);
    }
}

// Fehlt ein Scope, sagt die Antwort nach RFC 6750 welcher. Der Client weiss dann, dass er beim
// Kunden mehr Zustimmung einholen muss. Fehlt nur die Rolle, bleibt der Kopf weg: mehr Zustimmung
// des Kunden hilft dann nicht.
public sealed class InsufficientScopeResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _default = new AuthorizationMiddlewareResultHandler();

    public Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Forbidden)
        {
            string[] missing = policy.Requirements
                .OfType<ScopeRequirement>()
                .Where(requirement => !ScopeHandler.HasScope(context.User, requirement.Scope))
                .Select(requirement => requirement.Scope)
                .ToArray();

            if (missing.Length > 0)
            {
                context.Response.Headers.WWWAuthenticate =
                    $"Bearer error=\"insufficient_scope\", scope=\"{string.Join(' ', missing)}\"";
            }
        }

        return _default.HandleAsync(next, context, policy, authorizeResult);
    }
}

public static class ScopeAuthorization
{
    // Eine Policy je Scope, benannt wie der Scope: [Authorize(Policy = Scopes.AnalyticsRead)].
    public static IServiceCollection AddScopePolicies(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            foreach (string scope in Scopes.All)
            {
                options.AddPolicy(scope, policy => policy
                    .RequireAuthenticatedUser()
                    .AddRequirements(new ScopeRequirement(scope)));
            }
        });
        services.AddSingleton<IAuthorizationHandler, ScopeHandler>();
        services.AddSingleton<IAuthorizationMiddlewareResultHandler, InsufficientScopeResultHandler>();
        return services;
    }
}
