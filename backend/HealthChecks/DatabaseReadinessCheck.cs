using Eden_Relics_BE.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Eden_Relics_BE.HealthChecks;

/// <summary>
/// Readiness probe: confirms the app can actually reach Postgres. Exposed at <c>/readyz</c>, tagged
/// "ready". Deliberately NOT part of <c>/healthz</c> — Fly's machine health check must stay
/// liveness-only, because if it went DB-dependent a transient DB fault would make Fly conclude the
/// (healthy) API machines are unhealthy and restart/pull them, turning a recoverable DB blip into a
/// full outage and a restart storm. The uptime monitor polls <c>/readyz</c> instead, so a DB outage
/// pages us rather than silently surfacing as broken pages (which is exactly how the 2026-07-14
/// staging DB fault presented).
/// </summary>
public sealed class DatabaseReadinessCheck(EdenRelicsDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        // Cap the probe so a hung DB doesn't make /readyz itself hang past the poller's timeout.
        using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(5));

        try
        {
            bool canConnect = await db.Database.CanConnectAsync(cts.Token);
            return canConnect
                ? HealthCheckResult.Healthy("Database reachable.")
                : HealthCheckResult.Unhealthy("Database unreachable.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Database probe failed.", ex);
        }
    }
}
