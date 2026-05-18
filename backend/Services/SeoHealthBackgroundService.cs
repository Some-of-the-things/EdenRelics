namespace Eden_Relics_BE.Services;

/// <summary>
/// Captures an SEO health snapshot at startup (after a short delay) and every 24 hours.
/// Skips entirely when running under the Testing environment so test runs don't pollute.
/// </summary>
public class SeoHealthBackgroundService(
    SeoHealthService snapshotService,
    IHostEnvironment env,
    ILogger<SeoHealthBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan InitialDelay = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

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

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await snapshotService.CaptureAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to capture SEO health snapshot");
            }

            try
            {
                await Task.Delay(Interval, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                return;
            }
        }
    }
}
