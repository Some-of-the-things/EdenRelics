using System.Net;
using System.Net.Http.Json;
using static Eden_Relics_BE.Tests.Helpers;

namespace Eden_Relics_BE.Tests;

public class SeoHealthTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public SeoHealthTests(ApiFactory factory)
    {
        _factory = factory;
    }

    private record SnapshotResponse(
        Guid Id,
        DateTime TakenAtUtc,
        int TotalProducts,
        int LiveProducts,
        int StockProducts,
        int SoldProducts,
        int ProductsMissingImage,
        int ProductsMissingDescription,
        int ProductsMissingSlug,
        int ProductsMissingSku,
        int ProductsWithVideo,
        int ProductsWithAdditionalImages,
        int AvgProductDescriptionWords,
        int TotalBlogPosts,
        int PublishedBlogPosts,
        int BlogPostsMissingFeaturedImage,
        int BlogPostsMissingExcerpt,
        int AvgBlogPostWords,
        int SitemapUrlCount,
        int SitemapImageEntryCount,
        int TrackedKeywords,
        int TrackedKeywordsWithPosition,
        double AvgKeywordPosition,
        int KeywordsInTop10,
        int KeywordsInTop3
    );

    [Fact]
    public async Task GetSnapshots_Unauthenticated_Returns401()
    {
        HttpClient client = _factory.CreateClient();
        HttpResponseMessage response = await client.GetAsync("/api/seo/health/snapshots");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetSnapshots_AsCustomer_Returns403()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAndLogin(client, "customer-seohealth@test.com");
        HttpResponseMessage response = await client.GetAsync("/api/seo/health/snapshots");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetLatestSnapshot_NothingYet_Returns404()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "admin-seohealth-empty@test.com");
        HttpResponseMessage response = await client.GetAsync("/api/seo/health/snapshots/latest");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task RunSnapshot_PersistsAndReturnsCounts()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "admin-seohealth-run@test.com");

        HttpResponseMessage response = await client.PostAsync("/api/seo/health/snapshots/run", null);
        response.EnsureSuccessStatusCode();

        SnapshotResponse? snapshot = await response.Content.ReadFromJsonAsync<SnapshotResponse>(JsonOptions);
        Assert.NotNull(snapshot);
        // Seed has 10 Live products.
        Assert.True(snapshot.TotalProducts >= 10, $"Expected >=10 seeded products, got {snapshot.TotalProducts}");
        Assert.True(snapshot.LiveProducts >= 10, $"Expected >=10 live products, got {snapshot.LiveProducts}");
        Assert.True(snapshot.SitemapUrlCount >= snapshot.LiveProducts, "Sitemap URLs should at least cover live products");
        Assert.NotEqual(default, snapshot.TakenAtUtc);
    }

    [Fact]
    public async Task RunSnapshot_AppearsInGetSnapshots()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "admin-seohealth-list@test.com");

        HttpResponseMessage runResp = await client.PostAsync("/api/seo/health/snapshots/run", null);
        SnapshotResponse? created = await runResp.Content.ReadFromJsonAsync<SnapshotResponse>(JsonOptions);

        List<SnapshotResponse>? list = await client.GetFromJsonAsync<List<SnapshotResponse>>(
            "/api/seo/health/snapshots", JsonOptions);

        Assert.NotNull(list);
        Assert.Contains(list, s => s.Id == created!.Id);
    }

    [Fact]
    public async Task RunSnapshot_PicksUpNewStockProduct()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "admin-seohealth-stock@test.com");

        // Take a baseline.
        HttpResponseMessage baseResp = await client.PostAsync("/api/seo/health/snapshots/run", null);
        SnapshotResponse? baseline = await baseResp.Content.ReadFromJsonAsync<SnapshotResponse>(JsonOptions);

        // Add one Stock product.
        await client.PostAsJsonAsync("/api/products", new
        {
            name = "Hidden inventory",
            description = "This product is being held in stock and should not yet count toward live products.",
            price = 50m,
            era = "1990s",
            category = "90s",
            size = "10",
            condition = "good",
            imageUrl = "https://example.com/img.webp",
            inStock = true,
            status = "stock",
        });

        HttpResponseMessage afterResp = await client.PostAsync("/api/seo/health/snapshots/run", null);
        SnapshotResponse? after = await afterResp.Content.ReadFromJsonAsync<SnapshotResponse>(JsonOptions);

        Assert.NotNull(baseline);
        Assert.NotNull(after);
        Assert.Equal(baseline.StockProducts + 1, after.StockProducts);
        Assert.Equal(baseline.LiveProducts, after.LiveProducts);
        Assert.Equal(baseline.TotalProducts + 1, after.TotalProducts);
        // Stock products are not in sitemap.
        Assert.Equal(baseline.SitemapUrlCount, after.SitemapUrlCount);
    }

    [Fact]
    public async Task RunSnapshot_DetectsMissingPrimaryImage()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "admin-seohealth-missingimg@test.com");

        HttpResponseMessage baseResp = await client.PostAsync("/api/seo/health/snapshots/run", null);
        SnapshotResponse? baseline = await baseResp.Content.ReadFromJsonAsync<SnapshotResponse>(JsonOptions);

        await client.PostAsJsonAsync("/api/products", new
        {
            name = "No image product",
            description = "A description that is more than twenty words long so the description coverage check passes for this product specifically.",
            price = 50m,
            era = "1990s",
            category = "90s",
            size = "10",
            condition = "good",
            imageUrl = "",
            inStock = true,
            status = "live",
        });

        HttpResponseMessage afterResp = await client.PostAsync("/api/seo/health/snapshots/run", null);
        SnapshotResponse? after = await afterResp.Content.ReadFromJsonAsync<SnapshotResponse>(JsonOptions);

        Assert.NotNull(baseline);
        Assert.NotNull(after);
        Assert.Equal(baseline.ProductsMissingImage + 1, after.ProductsMissingImage);
    }

    [Fact]
    public async Task GetSnapshots_Take_ClampsToReasonableRange()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "admin-seohealth-take@test.com");

        // Asking for a giant take value should be clamped and still return 200.
        HttpResponseMessage response = await client.GetAsync("/api/seo/health/snapshots?take=99999");
        response.EnsureSuccessStatusCode();
    }
}
