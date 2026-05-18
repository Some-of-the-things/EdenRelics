using System.Text.RegularExpressions;
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
    ImageStorageService storage,
    IHttpClientFactory httpFactory,
    ILogger<ImageOptimizationService> logger)
{
    private const int MaxWidth = 800;
    private const int MaxHeight = 1000;
    private const int Quality = 75;

    private static readonly Regex VariantSuffixRegex = new(
        @"-(\d+)\.webp$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex ImgSrcRegex = new(
        """<img[^>]*src=["']([^"']+)["']""",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

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

        await UpdateDatabaseImageUrls();
    }

    public async Task BackfillImageVariantsAsync()
    {
        if (!storage.IsConfigured)
        {
            logger.LogInformation("Skipping variant backfill — R2 not configured");
            return;
        }

        string ownPrefix = storage.PublicUrl;
        if (string.IsNullOrEmpty(ownPrefix))
        {
            logger.LogWarning("Skipping variant backfill — R2 public URL not set");
            return;
        }

        using IServiceScope scope = scopeFactory.CreateScope();
        EdenRelicsDbContext db = scope.ServiceProvider.GetRequiredService<EdenRelicsDbContext>();

        List<Product> products = await db.Products.ToListAsync();
        List<BlogPost> blogPosts = await db.BlogPosts.ToListAsync();

        HashSet<string> candidates = new(StringComparer.OrdinalIgnoreCase);
        foreach (Product p in products)
        {
            TryAddCandidate(p.ImageUrl, candidates, ownPrefix);
            foreach (string u in p.AdditionalImageUrls)
            {
                TryAddCandidate(u, candidates, ownPrefix);
            }
        }
        foreach (BlogPost bp in blogPosts)
        {
            TryAddCandidate(bp.FeaturedImageUrl, candidates, ownPrefix);
            if (!string.IsNullOrEmpty(bp.Content))
            {
                foreach (Match m in ImgSrcRegex.Matches(bp.Content))
                {
                    TryAddCandidate(m.Groups[1].Value, candidates, ownPrefix);
                }
            }
        }

        if (candidates.Count == 0)
        {
            logger.LogInformation("No legacy images need variant backfill");
            return;
        }

        logger.LogInformation("Backfilling variants for {Count} legacy image(s)", candidates.Count);

        Dictionary<string, string> urlMap = new(StringComparer.OrdinalIgnoreCase);
        HttpClient http = httpFactory.CreateClient();

        foreach (string legacyUrl in candidates)
        {
            try
            {
                Uri uri = new(legacyUrl);
                string[] pathParts = uri.AbsolutePath.TrimStart('/').Split('/', 2);
                if (pathParts.Length != 2)
                {
                    continue;
                }
                string folder = pathParts[0];
                string baseName = Path.GetFileNameWithoutExtension(pathParts[1]);

                // Skip folders that intentionally use single-file uploads
                if (folder.Equals("branding", StringComparison.OrdinalIgnoreCase)
                    || folder.Equals("receipts", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                await using Stream sourceStream = await http.GetStreamAsync(legacyUrl);
                using Image source = await Image.LoadAsync(sourceStream);
                source.Mutate(x => x.AutoOrient());

                int largest = ImageUploadHelper.DefaultVariantWidths.Max();
                string newCanonical = "";

                foreach (int width in ImageUploadHelper.DefaultVariantWidths)
                {
                    using Image variant = source.Clone(_ => { });
                    if (variant.Width > width)
                    {
                        variant.Mutate(x => x.Resize(new ResizeOptions
                        {
                            Size = new Size(width, 0),
                            Mode = ResizeMode.Max,
                        }));
                    }

                    string fileName = $"{folder}/{baseName}-{width}.webp";
                    using MemoryStream output = new();
                    await variant.SaveAsync(output, new WebpEncoder { Quality = Quality });
                    output.Position = 0;

                    string url = await storage.UploadAsync(output, fileName, "image/webp");
                    if (width == largest)
                    {
                        newCanonical = url;
                    }
                }

                urlMap[legacyUrl] = newCanonical;
                logger.LogInformation("Backfilled {Legacy} -> {New}", legacyUrl, newCanonical);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to backfill variants for {Url}", legacyUrl);
            }
        }

        if (urlMap.Count == 0)
        {
            return;
        }

        foreach (Product p in products)
        {
            if (urlMap.TryGetValue(p.ImageUrl, out string? newPrimary))
            {
                p.ImageUrl = newPrimary;
            }
            if (p.AdditionalImageUrls.Count > 0)
            {
                List<string> updated = p.AdditionalImageUrls
                    .Select(u => urlMap.TryGetValue(u, out string? nu) ? nu : u)
                    .ToList();
                if (!updated.SequenceEqual(p.AdditionalImageUrls))
                {
                    p.AdditionalImageUrls = updated;
                }
            }
        }
        foreach (BlogPost bp in blogPosts)
        {
            if (bp.FeaturedImageUrl is not null
                && urlMap.TryGetValue(bp.FeaturedImageUrl, out string? newFeatured))
            {
                bp.FeaturedImageUrl = newFeatured;
            }
            if (!string.IsNullOrEmpty(bp.Content))
            {
                bp.Content = ImgSrcRegex.Replace(bp.Content, m =>
                {
                    string oldUrl = m.Groups[1].Value;
                    return urlMap.TryGetValue(oldUrl, out string? nu)
                        ? m.Value.Replace(oldUrl, nu)
                        : m.Value;
                });
            }
        }

        await db.SaveChangesAsync();
        logger.LogInformation("Variant backfill complete: {Count} URL(s) migrated", urlMap.Count);
    }

    private static void TryAddCandidate(string? url, HashSet<string> set, string ownPrefix)
    {
        if (string.IsNullOrEmpty(url))
        {
            return;
        }
        if (!url.StartsWith(ownPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }
        if (!url.EndsWith(".webp", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }
        if (VariantSuffixRegex.IsMatch(url))
        {
            return;
        }
        set.Add(url);
    }

    private async Task UpdateDatabaseImageUrls()
    {
        using IServiceScope scope = scopeFactory.CreateScope();
        EdenRelicsDbContext db = scope.ServiceProvider.GetRequiredService<EdenRelicsDbContext>();

        List<Product> products = await db.Products
            .Where(p => p.ImageUrl != null && !EF.Functions.Like(p.ImageUrl, "%.webp"))
            .ToListAsync();

        products = products
            .Where(p => new[] { ".jpg", ".jpeg", ".png", ".gif" }
                .Contains(Path.GetExtension(p.ImageUrl).ToLowerInvariant()))
            .ToList();

        foreach (Product product in products)
        {
            string oldExt = Path.GetExtension(product.ImageUrl);
            string newUrl = product.ImageUrl[..^oldExt.Length] + ".webp";

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
