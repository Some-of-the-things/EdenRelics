namespace Eden_Relics_BE.Services;

/// <summary>
/// Daily ingest of Search Console + GA4 data. Runs once at startup (after a
/// short delay to let the app settle) and then every 24 hours at 07:00 UTC,
/// which is after Google's typical ~2-3 day GSC processing window has updated
/// yesterday's partials.
/// Skipped under Testing so unit tests don't hammer the APIs.
/// </summary>
public class TrafficIngestBackgroundService(
    GoogleSearchConsoleService gsc,
    GoogleAnalyticsService ga4,
    IHostEnvironment env,
    ILogger<TrafficIngestBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan InitialDelay = TimeSpan.FromMinutes(3);

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
