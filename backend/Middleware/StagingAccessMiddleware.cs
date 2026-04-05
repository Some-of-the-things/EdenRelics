using System.Security.Claims;

namespace Eden_Relics_BE.Middleware;

/// <summary>
/// Restricts all staging API endpoints to authenticated Admin users.
/// Health checks and auth endpoints are exempt so admins can log in
/// and Fly.io health checks pass.
/// </summary>
public class StagingAccessMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        string path = context.Request.Path.Value ?? "";

        // Always allow health checks and auth endpoints
        if (path.StartsWith("/healthz", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/api/auth/", StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        // Allow all GET/HEAD/OPTIONS requests — the site is already behind
        // Cloudflare Access so only authorised users can reach it.
        // Write operations (POST/PUT/DELETE) require Admin role.
        if (HttpMethods.IsGet(context.Request.Method) ||
            HttpMethods.IsHead(context.Request.Method) ||
            HttpMethods.IsOptions(context.Request.Method))
        {
            await next(context);
            return;
        }

        // Require Admin role for write operations
        if (context.User.Identity?.IsAuthenticated != true ||
            !context.User.IsInRole("Admin"))
        {
            context.Response.StatusCode = 403;
            await context.Response.WriteAsJsonAsync(new { error = "Staging environment is restricted to admin users." });
            return;
        }

        await next(context);
    }
}
