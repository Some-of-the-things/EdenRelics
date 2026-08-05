using System.Threading.Channels;
using Eden_Relics_BE.Data.Entities;
using Eden_Relics_BE.Repositories;

namespace Eden_Relics_BE.Services;

/// <summary>
/// Emails "this piece is now on sale" to everyone who favourited a product and asked to hear
/// about reductions, on its own DI scope.
///
/// This exists because the controller used to do the work itself as fire-and-forget
/// (<c>_ = NotifySaleFavouritesAsync(product)</c>). The repositories it borrowed belong to the
/// REQUEST scope, which ASP.NET disposes as soon as the response completes — so the in-flight
/// favourites query died with ObjectDisposedException, the method's own catch logged it, and
/// nobody ever got the email. Queueing the product id and doing the work here on a fresh scope
/// is the same shape as <see cref="TranslationBackgroundService"/>, for the same reason.
/// </summary>
public class SaleNotificationBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<SaleNotificationBackgroundService> logger) : BackgroundService
{
    private static readonly Channel<Guid> Queue =
        Channel.CreateUnbounded<Guid>(new UnboundedChannelOptions { SingleReader = true });

    /// <summary>Queue a product whose sale price just changed. Safe to call from a request.</summary>
    public static void Enqueue(Guid productId)
    {
        Queue.Writer.TryWrite(productId);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (Guid productId in Queue.Reader.ReadAllAsync(stoppingToken))
            {
                await NotifyAsync(productId, scopeFactory, logger);
            }
        }
        catch (OperationCanceledException)
        {
            // Host is shutting down — exit gracefully.
        }
    }

    /// <summary>
    /// Processes everything queued so far and returns how many products were handled.
    /// Exposed for tests: the test host strips every IHostedService, so without this the
    /// notification path could only be asserted by proxy.
    /// </summary>
    public static async Task<int> DrainAsync(IServiceScopeFactory scopeFactory, ILogger logger)
    {
        int processed = 0;
        while (Queue.Reader.TryRead(out Guid productId))
        {
            await NotifyAsync(productId, scopeFactory, logger);
            processed++;
        }
        return processed;
    }

    private static async Task NotifyAsync(Guid productId, IServiceScopeFactory scopeFactory, ILogger logger)
    {
        try
        {
            using IServiceScope scope = scopeFactory.CreateScope();
            IRepository<Product> products = scope.ServiceProvider.GetRequiredService<IRepository<Product>>();
            IRepository<Favourite> favourites = scope.ServiceProvider.GetRequiredService<IRepository<Favourite>>();
            IRepository<User> users = scope.ServiceProvider.GetRequiredService<IRepository<User>>();
            IEmailService email = scope.ServiceProvider.GetRequiredService<IEmailService>();

            Product? product = await products.GetByIdAsync(productId);
            if (product?.SalePrice is null)
            {
                // Sale was cleared (or the piece deleted) between the edit and this run —
                // sending "now reduced" for a full-price item would be worse than silence.
                return;
            }

            IEnumerable<Favourite> interested = await favourites.FindAsync(
                f => f.ProductId == productId && f.NotifyOnSale);

            int sent = 0;
            foreach (Favourite fav in interested)
            {
                User? user = await users.GetByIdAsync(fav.UserId);
                if (user is null)
                {
                    continue;
                }
                // Awaited, not fire-and-forget: a send that fails should be logged, not lost.
                await email.SendSaleNotificationAsync(
                    user.Email, user.FirstName, product.Name, product.Price, product.SalePrice.Value);
                sent++;
            }

            if (sent > 0)
            {
                logger.LogInformation(
                    "Sent {Count} sale notification(s) for product {ProductId} ({Name})",
                    sent, productId, product.Name);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send sale notifications for product {ProductId}", productId);
        }
    }
}
