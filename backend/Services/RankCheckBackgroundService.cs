using Eden_Relics_BE.Data;

namespace Eden_Relics_BE.Services;

public class RankCheckBackgroundService(IServiceScopeFactory scopeFactory, ILogger<RankCheckBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait for app startup to complete
        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            TimeSpan untilNext = TimeUntilNextRun();
            logger.LogInformation("Next rank check in {Hours:F1} hours", untilNext.TotalHours);
            await Task.Delay(untilNext, stoppingToken);

            try
            {
                using IServiceScope scope = scopeFactory.CreateScope();
                RankCheckerService checker = scope.ServiceProvider.GetRequiredService<RankCheckerService>();
                EdenRelicsDbContext context = scope.ServiceProvider.GetRequiredService<EdenRelicsDbContext>();
                await checker.CheckAllKeywordsAsync(context);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Daily rank check failed");
            }
        }
    }

    private static TimeSpan TimeUntilNextRun()
    {
        // Run at 06:00 UTC daily
        DateTime now = DateTime.UtcNow;
        DateTime nextRun = now.Date.AddHours(6);
        if (nextRun <= now)
        {
            nextRun = nextRun.AddDays(1);
        }
        return nextRun - now;
    }
}
