namespace Eden_Relics_BE.Services;

/// <summary>
/// Works the newsletter queue (scheduled sends that are due, batches in progress) and sends
/// review requests that are due. Polls every 30 seconds; a "send now" wakes it straight away.
/// Review requests only need checking every few minutes, so they run on every tenth pass.
/// </summary>
public class NewsletterBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<NewsletterBackgroundService> logger) : BackgroundService
{
    private static readonly SemaphoreSlim Signal = new(0, 1);
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    /// <summary>Wakes the worker now (a newsletter was just sent).</summary>
    public static void Wake()
    {
        if (Signal.CurrentCount == 0)
        {
            try
            {
                Signal.Release();
            }
            catch (SemaphoreFullException)
            {
                // Already signalled.
            }
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Let startup (migrations, seeds) finish first.
        await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken).ContinueWith(_ => { }, CancellationToken.None);

        int pass = 0;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using IServiceScope scope = scopeFactory.CreateScope();
                INewsletterService newsletters = scope.ServiceProvider.GetRequiredService<INewsletterService>();
                await newsletters.ProcessQueueAsync(stoppingToken);

                if (pass % 10 == 0)
                {
                    IReviewRequestService reviews = scope.ServiceProvider.GetRequiredService<IReviewRequestService>();
                    await reviews.SendDueAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Newsletter/review-request worker pass failed");
            }

            pass++;
            try
            {
                await Signal.WaitAsync(Interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }
}
