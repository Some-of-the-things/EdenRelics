using Microsoft.Extensions.Options;

namespace Eden_Relics_BE.Services;

/// <summary>
/// Hourly worker job that releases due seller payouts (separate charges & transfers, held until the
/// 14-day window closes). Runs only on the worker instance (registered behind runScheduledJobs) and
/// is a **no-op while the marketplace is gated** (Marketplace:Enabled = false) — so it does nothing
/// until launch, and nothing at all until the checkout webhook starts creating payout rows (a
/// separate, deferred change). Safe to ship dormant.
/// </summary>
public class SellerPayoutReleaseService(
    IServiceScopeFactory scopeFactory,
    IOptions<MarketplaceOptions> marketplace,
    ILogger<SellerPayoutReleaseService> logger) : BackgroundService
{
    private static readonly TimeSpan BootDelay = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(BootDelay, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (marketplace.Value.Enabled)
                {
                    using IServiceScope scope = scopeFactory.CreateScope();
                    ISellerPayoutService service = scope.ServiceProvider.GetRequiredService<ISellerPayoutService>();
                    int released = await service.ReleaseDuePayoutsAsync();
                    if (released > 0)
                    {
                        logger.LogInformation("Released {Count} seller payout(s).", released);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Seller payout release run failed.");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }
}
