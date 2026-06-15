namespace Eden_Relics_BE.Services;

public class MonzoSyncBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<MonzoSyncBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using IServiceScope scope = scopeFactory.CreateScope();
                IMonzoService monzo = scope.ServiceProvider.GetRequiredService<IMonzoService>();
                await monzo.RunScheduledSyncAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Monzo sync failed");
            }

            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }
}
