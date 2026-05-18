namespace Eden_Relics_BE.Data.Entities;

/// <summary>
/// Periodic capture of internal SEO-related signals. Lets us compare
/// catalog quality and surface counts over time without polling Google.
/// </summary>
public class SeoHealthSnapshot : BaseEntity
{
    public DateTime TakenAtUtc { get; set; }

    // Catalog signals
    public int TotalProducts { get; set; }
    public int LiveProducts { get; set; }
    public int StockProducts { get; set; }
    public int SoldProducts { get; set; }
    public int ProductsMissingImage { get; set; }
    public int ProductsMissingDescription { get; set; }
    public int ProductsMissingSlug { get; set; }
    public int ProductsMissingSku { get; set; }
    public int ProductsWithVideo { get; set; }
    public int ProductsWithAdditionalImages { get; set; }
    public int AvgProductDescriptionWords { get; set; }

    // Blog signals
    public int TotalBlogPosts { get; set; }
    public int PublishedBlogPosts { get; set; }
    public int BlogPostsMissingFeaturedImage { get; set; }
    public int BlogPostsMissingExcerpt { get; set; }
    public int AvgBlogPostWords { get; set; }

    // Sitemap signals (live products only)
    public int SitemapUrlCount { get; set; }
    public int SitemapImageEntryCount { get; set; }

    // Keyword tracking
    public int TrackedKeywords { get; set; }
    public int TrackedKeywordsWithPosition { get; set; }
    public double AvgKeywordPosition { get; set; }
    public int KeywordsInTop10 { get; set; }
    public int KeywordsInTop3 { get; set; }
}
