using Analytics.Api.Authentication;
using Microsoft.Extensions.Options;

namespace Analytics.Api.Frontend;

// Die Statistikseite liegt unter wwwroot und wird von diesem Dienst selbst ausgeliefert: gleiche
// Herkunft wie die API, also kein CORS. Sie ist kein Teil des API-Kontrakts.
public static class FrontendEndpoints
{
    public const string ConfigPath = "/frontend/config.json";

    public static IServiceCollection AddFrontend(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<FrontendOptions>(configuration.GetSection(FrontendOptions.SectionName));
        return services;
    }

    public static WebApplication UseFrontend(this WebApplication app)
    {
        app.UseFrontendSecurityHeaders();
        app.UseDefaultFiles();

        // no-cache heisst: der Browser darf die Datei behalten, fragt aber jedes Mal mit ETag nach.
        // Ohne das zeigte er nach einem neuen Image noch tagelang die alten Module.
        app.UseStaticFiles(new StaticFileOptions
        {
            OnPrepareResponse = context => context.Context.Response.Headers.CacheControl = "no-cache"
        });
        return app;
    }

    public static WebApplication MapFrontend(this WebApplication app)
    {
        app.MapGet(ConfigPath, (IOptions<AuthSettings> auth, IOptions<FrontendOptions> frontend) => Results.Ok(new
        {
            issuer = auth.Value.Issuer,
            clientId = frontend.Value.ClientId
        }))
            .AllowAnonymous()
            .ExcludeFromDescription();

        return app;
    }

    // Swagger bringt eigene Inline-Skripte mit und bekommt deshalb keine CSP. Die Seite selbst lädt
    // nur eigene Dateien und spricht ausser mit der API nur mit dem Keycloak.
    private static void UseFrontendSecurityHeaders(this WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            if (!context.Request.Path.StartsWithSegments("/swagger"))
            {
                string issuer = context.RequestServices.GetRequiredService<IOptions<AuthSettings>>().Value.Issuer;
                string keycloak = new Uri(issuer).GetLeftPart(UriPartial.Authority);

                IHeaderDictionary headers = context.Response.Headers;
                headers.ContentSecurityPolicy =
                    $"default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:; " +
                    $"connect-src 'self' {keycloak}; frame-ancestors 'none'; base-uri 'self'; form-action 'self'";
                headers.XContentTypeOptions = "nosniff";

                // Der Autorisierungscode steht nach dem Login kurz in der Adresse und darf nicht
                // als Referrer an Dritte gehen.
                headers["Referrer-Policy"] = "no-referrer";
            }

            await next();
        });
    }
}
