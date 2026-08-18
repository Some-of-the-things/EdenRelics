using Microsoft.AspNetCore.Authorization;

namespace EdenRelics.SellerTool.Api;

/// <summary>
/// The closed-beta gate.
///
/// The tool validates the main site's JWT, and an ordinary customer account carries one — so the
/// adminGuard on the Angular /seller-tool route was never the boundary, only the signpost. Anyone
/// with an Eden Relics login and this API's URL could call it directly. The boundary is here.
///
/// Admins always pass, because Teodora assesses the tool on an admin account and closing the gate
/// must not close it on her. Everyone else passes only once the beta is open.
/// </summary>
public sealed class ToolAccessRequirement : IAuthorizationRequirement;

/// <summary>
/// Decides <see cref="ToolAccessRequirement"/> per request rather than at startup.
///
/// Deliberately not a flag captured when the host is built: that reads before a test host can
/// override configuration, so the gate would silently ignore its own setting and every test would
/// run against whichever value happened to win. Resolving it per request also means the beta can be
/// opened by configuration alone.
/// </summary>
public sealed class ToolAccessHandler(IConfiguration configuration)
    : AuthorizationHandler<ToolAccessRequirement>
{
    /// <summary>
    /// Defaults to CLOSED. Opening the seller beta is two deliberate changes — this flag, and
    /// adminGuard -> sellerGuard on the /seller-tool route. Defaulting closed means forgetting one
    /// of them leaves the tool locked rather than open to every customer with an account.
    /// </summary>
    public const string AdminOnlyKey = "Tool:AdminOnly";

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, ToolAccessRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            // Leave it unmet: an unauthenticated caller should be challenged (401), not told it is
            // forbidden (403) — the difference is "sign in" versus "this is not for you".
            return Task.CompletedTask;
        }

        bool adminOnly = configuration.GetValue(AdminOnlyKey, true);
        if (!adminOnly || context.User.IsInRole("Admin"))
        {
            context.Succeed(requirement);
        }
        return Task.CompletedTask;
    }
}
