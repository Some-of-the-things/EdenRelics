using Eden_Relics_BE.Data;
using Eden_Relics_BE.Data.Entities;
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Webp;

namespace Eden_Relics_BE.Services;

public class ImageOptimizationService(
    IWebHostEnvironment env,
    IServiceScopeFactory scopeFactory,
    ILogger<ImageOptimizationService> logger)
{
    private const int MaxWidth = 800;
    private const int MaxHeight = 1000;
    private const int Quality = 75;

    public async Task OptimizeExistingImagesAsync()
    {
        string uploadsDir = Path.Combine(env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot"), "uploads");
        if (!Directory.Exists(uploadsDir))
        {
            return;
        }

        string[] imageFiles = Directory.GetFiles(uploadsDir)
            .Where(f => !f.EndsWith(".webp", StringComparison.OrdinalIgnoreCase))
            .Where(f => new[] { ".jpg", ".jpeg", ".png", ".gif" }
                .Contains(Path.GetExtension(f).ToLowerInvariant()))
            .ToArray();

        if (imageFiles.Length == 0)
        {
            logger.LogInformation("No unoptimized images found");
            return;
        }

        logger.LogInformation("Optimizing {Count} images...", imageFiles.Length);

        foreach (string filePath in imageFiles)
        {
            try
            {
                string webpPath = Path.ChangeExtension(filePath, ".webp");
                if (File.Exists(webpPath))
                {
                    continue;
                }

                using Image image = await Image.LoadAsync(filePath);
                image.Mutate(x => x.AutoOrient());

                if (image.Width > MaxWidth || image.Height > MaxHeight)
                {
                    image.Mutate(x => x.Resize(new ResizeOptions
                    {
                        Size = new Size(MaxWidth, MaxHeight),
                        Mode = ResizeMode.Max,
                    }));
                }

                await image.SaveAsync(webpPath, new WebpEncoder { Quality = Quality });

                long originalSize = new FileInfo(filePath).Length;
                long newSize = new FileInfo(webpPath).Length;
                logger.LogInformation("Optimized {File}: {Original}kB -> {New}kB",
                    Path.GetFileName(filePath),
                    originalSize / 1024,
                    newSize / 1024);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to optimize {File}", Path.GetFileName(filePath));
            }
        }

        // Update database references to use .webp versions
        await UpdateDatabaseImageUrls();
    }

    private async Task UpdateDatabaseImageUrls()
    {
        using IServiceScope scope = scopeFactory.CreateScope();
        EdenRelicsDbContext db = scope.ServiceProvider.GetRequiredService<EdenRelicsDbContext>();

        List<Product> products = await db.Products
            .Where(p => p.ImageUrl != null && !EF.Functions.Like(p.ImageUrl, "%.webp"))
            .ToListAsync();

        // Filter client-side for image extensions
        products = products
            .Where(p => new[] { ".jpg", ".jpeg", ".png", ".gif" }
                .Contains(Path.GetExtension(p.ImageUrl).ToLowerInvariant()))
            .ToList();

        foreach (Product product in products)
        {
            string oldExt = Path.GetExtension(product.ImageUrl);
            string newUrl = product.ImageUrl[..^oldExt.Length] + ".webp";

            // Only update if the webp file actually exists
            string fileName = Path.GetFileName(new Uri(newUrl).LocalPath);
            string uploadsDir = Path.Combine(env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot"), "uploads");
            if (File.Exists(Path.Combine(uploadsDir, fileName)))
            {
                product.ImageUrl = newUrl;
                logger.LogInformation("Updated DB: {Product} -> {Url}", product.Name, newUrl);
            }
        }

        if (products.Count > 0)
        {
            await db.SaveChangesAsync();
        }
    }
}
