using Eden_Relics_BE.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Eden_Relics_BE.Services;

/// <summary>
/// Daily ingest of Search Console + GA4 data. Runs once at startup (after a
/// short delay to let the app settle) and then every 24 hours at 07:00 UTC,
/// which is after Google's typical ~2-3 day GSC processing window has updated
/// yesterday's partials.
/// Skipped under Testing so unit tests don't hammer the APIs.
/// After each run it checks data freshness and emails the operator if a feed
/// has gone stale (the usual cause being an expired OAuth refresh token) — so a
/// silent ingest failure surfaces within a day instead of going unnoticed.
/// </summary>
public class TrafficIngestBackgroundService(
    GoogleSearchConsoleService gsc,
    GoogleAnalyticsService ga4,
    IServiceScopeFactory scopeFactory,
    IConfiguration config,
    IHostEnvironment env,
    ILogger<TrafficIngestBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan InitialDelay = TimeSpan.FromMinutes(3);

    /// <summary>A feed is stale once its newest day is older than this many days behind today.</summary>
    private const int StaleDays = 3;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (env.EnvironmentName == "Testing")
        {
            return;
        }

        try
        {
            await Task.Delay(InitialDelay, stoppingToken);
        }
        catch (TaskCanceledException)
        {
            return;
        }

        // Initial sync on startup.
        await RunOnceAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            TimeSpan untilNext = TimeUntilNextRun();
            logger.LogInformation("Next traffic ingest in {Hours:F1} hours", untilNext.TotalHours);
            try
            {
                await Task.Delay(untilNext, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                return;
            }

            await RunOnceAsync(stoppingToken);
        }
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        try
        {
            await gsc.SyncAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "GSC sync failed");
        }

        try
        {
            await ga4.SyncAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "GA4 sync failed");
        }

        await CheckStalenessAndAlertAsync(ct);
    }

    /// <summary>
    /// After a sync, if either feed's newest data is older than <see cref="StaleDays"/>
    /// days, log a warning and email the operator. Runs once per ingest (daily), so a
    /// persistent outage produces at most one nudge per day until it's fixed.
    /// </summary>
    private async Task CheckStalenessAndAlertAsync(CancellationToken ct)
    {
        try
        {
            using IServiceScope scope = scopeFactory.CreateScope();
            EdenRelicsDbContext db = scope.ServiceProvider.GetRequiredService<EdenRelicsDbContext>();

            DateOnly? gscLast = await db.SearchConsoleDailyTotals
                .OrderByDescending(t => t.Date).Select(t => (DateOnly?)t.Date).FirstOrDefaultAsync(ct);
            DateOnly? ga4Last = await db.AnalyticsDailyTotals
                .OrderByDescending(t => t.Date).Select(t => (DateOnly?)t.Date).FirstOrDefaultAsync(ct);

            DateOnly staleBefore = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-StaleDays);
            List<string> problems = [];
            if (gsc.IsConfigured && (gscLast is null || gscLast < staleBefore))
            {
                problems.Add($"Search Console — latest data: {gscLast?.ToString("yyyy-MM-dd") ?? "none"}");
            }
            if (ga4.IsConfigured && (ga4Last is null || ga4Last < staleBefore))
            {
                problems.Add($"Analytics (GA4) — latest data: {ga4Last?.ToString("yyyy-MM-dd") ?? "none"}");
            }
            if (problems.Count == 0)
            {
                return;
            }

            logger.LogWarning("Traffic ingest stale: {Problems}", string.Join("; ", problems));

            string? recipient = config["Email:SaleNotificationRecipient"];
            if (string.IsNullOrWhiteSpace(recipient))
            {
                return;
            }

            IEmailService email = scope.ServiceProvider.GetRequiredService<IEmailService>();
            string body =
                "The Eden Relics traffic ingest looks stale — Google data hasn't updated as expected:\n\n"
                + string.Join("\n", problems)
                + "\n\nThe usual cause is the shared Google OAuth refresh token expiring or being revoked. "
                + "Re-mint it and update the Google__OAuth__RefreshToken secret on eden-relics-api. "
                + "Current freshness is shown on the admin SEO tab.";
            await email.SendOperatorReminderEmailAsync(recipient, "Traffic ingest is stale", body);
            logger.LogInformation("Sent traffic-staleness alert to {Recipient}", recipient);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Traffic staleness check failed");
        }
    }

    private static TimeSpan TimeUntilNextRun()
    {
        DateTime now = DateTime.UtcNow;
        DateTime nextRun = now.Date.AddHours(7);
        if (nextRun <= now)
        {
            nextRun = nextRun.AddDays(1);
        }
        return nextRun - now;
    }
}
