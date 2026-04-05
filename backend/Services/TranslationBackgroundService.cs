using System.Threading.Channels;
using Eden_Relics_BE.Data;
using Eden_Relics_BE.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Eden_Relics_BE.Services;

/// <summary>
/// Processes translation work items in the background with its own DbContext scope,
/// avoiding the disposal issues caused by fire-and-forget patterns in controllers.
/// </summary>
public class TranslationBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<TranslationBackgroundService> logger) : BackgroundService
{
    private static readonly Channel<TranslationWorkItem> Queue =
        Channel.CreateUnbounded<TranslationWorkItem>(new UnboundedChannelOptions { SingleReader = true });

    /// <summary>Enqueue a product for translation.</summary>
    public static void EnqueueProduct(Guid productId)
    {
        Queue.Writer.TryWrite(new TranslationWorkItem(TranslationKind.Product, productId));
    }

    /// <summary>Enqueue CMS content for translation.</summary>
    public static void EnqueueContent(Dictionary<string, string> content)
    {
        Queue.Writer.TryWrite(new TranslationWorkItem(TranslationKind.Content, null, content));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (TranslationWorkItem item in Queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                using IServiceScope scope = scopeFactory.CreateScope();
                TranslationService translation = scope.ServiceProvider.GetRequiredService<TranslationService>();
                EdenRelicsDbContext context = scope.ServiceProvider.GetRequiredService<EdenRelicsDbContext>();

                switch (item.Kind)
                {
                    case TranslationKind.Product:
                        await TranslateProductAsync(item.ProductId!.Value, translation, context);
                        break;
                    case TranslationKind.Content:
                        await TranslateContentAsync(item.Content!, translation, context);
                        break;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to process translation work item: {Kind}", item.Kind);
            }
        }
    }

    private async Task TranslateProductAsync(Guid productId, TranslationService translation, EdenRelicsDbContext context)
    {
        Product? product = await context.Products.FindAsync(productId);
        if (product is null)
        {
            return;
        }

        Dictionary<string, string> nameTranslations = await translation.TranslateTextAsync(product.Name);
        Dictionary<string, string> descTranslations = await translation.TranslateTextAsync(product.Description);

        product.NameTranslations = nameTranslations;
        product.DescriptionTranslations = descTranslations;
        await context.SaveChangesAsync();

        logger.LogInformation("Translated product {ProductId} ({Name})", productId, product.Name);
    }

    private async Task TranslateContentAsync(Dictionary<string, string> content, TranslationService translation, EdenRelicsDbContext context)
    {
        Dictionary<string, string> translations = await translation.TranslateBatchAsync(content);
        if (translations.Count == 0)
        {
            return;
        }

        List<SiteContent> existing = await context.SiteContent.ToListAsync();
        Dictionary<string, SiteContent> byKey = existing.ToDictionary(e => e.Key);

        foreach ((string key, string value) in translations)
        {
            if (byKey.TryGetValue(key, out SiteContent? entry))
            {
                entry.Value = value;
            }
            else
            {
                context.SiteContent.Add(new SiteContent { Key = key, Value = value });
            }
        }

        await context.SaveChangesAsync();
        logger.LogInformation("Translated {Count} content entries", translations.Count);
    }

    private record TranslationWorkItem(
        TranslationKind Kind,
        Guid? ProductId = null,
        Dictionary<string, string>? Content = null);

    private enum TranslationKind { Product, Content }
}
