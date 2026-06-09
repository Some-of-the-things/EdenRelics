using System.Text.RegularExpressions;
using Eden_Relics_BE.Data;
using Eden_Relics_BE.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Eden_Relics_BE.Services;

public partial class SeoHealthService(
    IServiceScopeFactory scopeFactory,
    ILogger<SeoHealthService> logger)
{
    [GeneratedRegex(@"\b\w+\b")]
    private static partial Regex WordRegex();

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex HtmlTagRegex();

    /// <summary>
    /// Capture and persist a fresh snapshot of internal SEO signals. Idempotent
    /// at the row level (each call inserts a new row, timestamped).
    /// </summary>
    public async Task<SeoHealthSnapshot> CaptureAsync(CancellationToken ct = default)
    {
        using IServiceScope scope = scopeFactory.CreateScope();
        EdenRelicsDbContext db = scope.ServiceProvider.GetRequiredService<EdenRelicsDbContext>();
        SitemapRoutesService sitemapRoutes = scope.ServiceProvider.GetRequiredService<SitemapRoutesService>();

        List<Product> products = await db.Products.ToListAsync(ct);
        List<BlogPost> blogPosts = await db.BlogPosts.ToListAsync(ct);
        List<TrackedKeyword> keywords = await db.TrackedKeywords.ToListAsync(ct);

        SeoHealthSnapshot snapshot = new()
        {
            TakenAtUtc = DateTime.UtcNow,
        };

        // Catalog
        snapshot.TotalProducts = products.Count;
        snapshot.LiveProducts = products.Count(p => p.Status == ProductStatus.Live);
        snapshot.StockProducts = products.Count(p => p.Status == ProductStatus.Stock);
        snapshot.SoldProducts = products.Count(p => p.Status == ProductStatus.Sold);
        snapshot.ProductsMissingImage = products.Count(p => string.IsNullOrWhiteSpace(p.ImageUrl));
        snapshot.ProductsMissingDescription = products.Count(p => CountWords(p.Description) < 20);
        snapshot.ProductsMissingSlug = products.Count(p => string.IsNullOrWhiteSpace(p.Slug));
        snapshot.ProductsMissingSku = products.Count(p => string.IsNullOrWhiteSpace(p.Sku));
        snapshot.ProductsWithVideo = products.Count(p => p.VideoUrls.Count > 0);
        snapshot.ProductsWithAdditionalImages = products.Count(p => p.AdditionalImageUrls.Count > 0);
        snapshot.AvgProductDescriptionWords = products.Count > 0
            ? (int)Math.Round(products.Average(p => (double)CountWords(p.Description)))
            : 0;

        // Blog
        snapshot.TotalBlogPosts = blogPosts.Count;
        snapshot.PublishedBlogPosts = blogPosts.Count(b => b.Published);
        snapshot.BlogPostsMissingFeaturedImage = blogPosts.Count(b => string.IsNullOrWhiteSpace(b.FeaturedImageUrl));
        snapshot.BlogPostsMissingExcerpt = blogPosts.Count(b => string.IsNullOrWhiteSpace(b.Excerpt));
        snapshot.AvgBlogPostWords = blogPosts.Count > 0
            ? (int)Math.Round(blogPosts.Average(b => (double)CountWords(b.Content)))
            : 0;

        // Sitemap — mirror SitemapController exactly: live (non-deleted) products only
        // (NOT sold/stock), published posts, and the static routes the frontend serves.
        List<Product> liveSitemapProducts = products.Where(p => p.Status == ProductStatus.Live).ToList();
        int productSitemapImages = liveSitemapProducts.Sum(p =>
            (string.IsNullOrWhiteSpace(p.ImageUrl) ? 0 : 1) + p.AdditionalImageUrls.Count);
        int blogSitemapImages = blogPosts.Where(b => b.Published)
            .Count(b => !string.IsNullOrWhiteSpace(b.FeaturedImageUrl));
        // Static page count comes from the same source the sitemap uses, so it never drifts.
        int staticPageCount = (await sitemapRoutes.GetAsync()).Count;
        snapshot.SitemapUrlCount = staticPageCount + liveSitemapProducts.Count + blogPosts.Count(b => b.Published);
        snapshot.SitemapImageEntryCount = productSitemapImages + blogSitemapImages;

        // Keywords
        snapshot.TrackedKeywords = keywords.Count;
        List<int> ranks = keywords.Where(k => k.LastPosition.HasValue)
            .Select(k => k.LastPosition!.Value).ToList();
        snapshot.TrackedKeywordsWithPosition = ranks.Count;
        snapshot.AvgKeywordPosition = ranks.Count > 0 ? Math.Round(ranks.Average(), 1) : 0;
        snapshot.KeywordsInTop10 = ranks.Count(r => r > 0 && r <= 10);
        snapshot.KeywordsInTop3 = ranks.Count(r => r > 0 && r <= 3);

        db.SeoHealthSnapshots.Add(snapshot);
        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "SEO health snapshot {Id} captured at {Taken}: {Live} live / {Stock} stock / {Sold} sold products, {Posts} blog posts, sitemap {Urls} urls / {Imgs} images.",
            snapshot.Id, snapshot.TakenAtUtc, snapshot.LiveProducts, snapshot.StockProducts,
            snapshot.SoldProducts, snapshot.PublishedBlogPosts, snapshot.SitemapUrlCount, snapshot.SitemapImageEntryCount);

        return snapshot;
    }

    private static int CountWords(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return 0;
        }
        string plain = HtmlTagRegex().Replace(html, " ");
        return WordRegex().Matches(plain).Count;
    }
}
