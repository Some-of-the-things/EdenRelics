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
        // Cap the whole probe so a hung DB doesn't make /readyz itself hang past the poller's timeout.
        using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(5));

        // One transient blip on the single-node DB (a momentary network/connection hiccup) shouldn't
        // flip readiness and page us — retry once within the budget before declaring it unreachable.
        // A genuine sustained outage fails both attempts and still reports Unhealthy.
        Exception? last = null;
        for (int attempt = 1; attempt <= 2; attempt++)
        {
            try
            {
                if (await db.Database.CanConnectAsync(cts.Token))
                {
                    return HealthCheckResult.Healthy("Database reachable.");
                }
            }
            catch (Exception ex)
            {
                last = ex;
            }

            if (cts.IsCancellationRequested)
            {
                break;
            }

            if (attempt == 1)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(250), cts.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        return HealthCheckResult.Unhealthy("Database unreachable.", last);
    }
}
