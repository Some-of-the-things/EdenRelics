namespace Eden_Relics_BE.Services;

/// <summary>
/// Background service that keeps the rolling 12-month obligation window populated. Runs once
/// shortly after boot (30s delay so the boot-time DB migration finishes first) and then every
/// 6 hours. All work is delegated to <see cref="ILiabilityScheduleService"/>, which is idempotent.
/// </summary>
public sealed class LiabilityScheduleHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<LiabilityScheduleHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan BootDelay = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan Interval = TimeSpan.FromHours(6);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(BootDelay, stoppingToken);
        }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
                ILiabilityScheduleService svc = scope.ServiceProvider.GetRequiredService<ILiabilityScheduleService>();
                await svc.EnsureUpcomingAsync(DateTime.UtcNow, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception e)
            {
                logger.LogError(e, "LiabilityScheduleHostedService cycle failed");
            }

            try
            {
                await Task.Delay(Interval, stoppingToken);
            }
            catch (OperationCanceledException) { return; }
        }
    }
}
