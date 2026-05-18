using System.Net.Http.Json;
using static Eden_Relics_BE.Tests.Helpers;

namespace Eden_Relics_BE.Tests;

public class SitemapTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public SitemapTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Sitemap_DeclaresImageNamespace()
    {
        HttpClient client = _factory.CreateClient();
        HttpResponseMessage response = await client.GetAsync("/api/sitemap.xml");
        response.EnsureSuccessStatusCode();
        string xml = await response.Content.ReadAsStringAsync();

        Assert.Contains("xmlns:image=\"http://www.google.com/schemas/sitemap-image/1.1\"", xml);
    }

    [Fact]
    public async Task Sitemap_IncludesProductImageEntries()
    {
        HttpClient client = _factory.CreateClient();
        HttpResponseMessage response = await client.GetAsync("/api/sitemap.xml");
        string xml = await response.Content.ReadAsStringAsync();

        // Seeded products have placehold.co image URLs — every product url block
        // should also emit at least one <image:image> with <image:loc>.
        Assert.Contains("<image:image>", xml);
        Assert.Contains("<image:loc>", xml);
    }

    [Fact]
    public async Task Sitemap_AddsImagesForUploadedProduct()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "admin-sitemap-img@test.com");

        await client.PostAsJsonAsync("/api/products", new
        {
            name = "Sitemap Test Item",
            description = "for sitemap test",
            price = 50m,
            era = "1990s",
            category = "90s",
            size = "10",
            condition = "good",
            imageUrl = "https://example.com/sitemap-test-primary.webp",
            additionalImageUrls = new[]
            {
                "https://example.com/sitemap-test-extra-1.webp",
                "https://example.com/sitemap-test-extra-2.webp",
            },
            inStock = true,
            status = "live",
        });

        HttpClient anon = _factory.CreateClient();
        HttpResponseMessage response = await anon.GetAsync("/api/sitemap.xml");
        string xml = await response.Content.ReadAsStringAsync();

        Assert.Contains("sitemap-test-primary.webp", xml);
        Assert.Contains("sitemap-test-extra-1.webp", xml);
        Assert.Contains("sitemap-test-extra-2.webp", xml);
    }

    [Fact]
    public async Task Sitemap_DoesNotLeakStockProductImages()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "admin-sitemap-stock@test.com");

        await client.PostAsJsonAsync("/api/products", new
        {
            name = "Hidden Stock Item",
            description = "stealth",
            price = 50m,
            era = "1990s",
            category = "90s",
            size = "10",
            condition = "good",
            imageUrl = "https://example.com/should-not-appear.webp",
            inStock = true,
            status = "stock",
        });

        HttpClient anon = _factory.CreateClient();
        HttpResponseMessage response = await anon.GetAsync("/api/sitemap.xml");
        string xml = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("should-not-appear.webp", xml);
    }
}
