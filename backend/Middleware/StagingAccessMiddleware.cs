using System.Security.Claims;

namespace Eden_Relics_BE.Middleware;

/// <summary>
/// Restricts all staging API endpoints to authenticated Admin users.
/// Health checks and auth endpoints are exempt so admins can log in
/// and Fly.io health checks pass.
/// </summary>
public class StagingAccessMiddleware(RequestDelegate next)
{
    private static readonly string[] ExemptPrefixes =
    [
        "/healthz",
        "/api/auth/",
        "/api/content",   // Allow content for the login page to render
        "/api/branding",  // Allow branding for the staging site to load
    ];

    public async Task InvokeAsync(HttpContext context)
    {
        string path = context.Request.Path.Value ?? "";

        // Allow exempt endpoints without auth
        if (ExemptPrefixes.Any(prefix => path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
        {
            await next(context);
            return;
        }

        // Require Admin role for everything else
        if (!context.User.Identity?.IsAuthenticated == true ||
            !context.User.IsInRole("Admin"))
        {
            context.Response.StatusCode = 403;
            await context.Response.WriteAsJsonAsync(new { error = "Staging environment is restricted to admin users." });
            return;
        }

        await next(context);
    }
}
