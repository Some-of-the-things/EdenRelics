using System.Net.Http.Json;
using static Eden_Relics_BE.Tests.Helpers;

namespace Eden_Relics_BE.Tests;

public class MerchantFeedTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public MerchantFeedTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Feed_DeclaresRssAndGoogleNamespace()
    {
        HttpClient client = _factory.CreateClient();
        HttpResponseMessage response = await client.GetAsync("/api/merchant-feed.xml");
        response.EnsureSuccessStatusCode();
        string xml = await response.Content.ReadAsStringAsync();

        Assert.Contains("<rss version=\"2.0\"", xml);
        Assert.Contains("xmlns:g=\"http://base.google.com/ns/1.0\"", xml);
    }

    [Fact]
    public async Task Feed_IncludesLiveProductWithRequiredAttributes()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "admin-feed-live@test.com");

        await client.PostAsJsonAsync("/api/products", new
        {
            name = "Feed Test Navy Dress",
            description = "A <strong>lovely</strong> vintage piece.<br>Fabric: rayon.",
            price = 42m,
            era = "1970s",
            category = "70s",
            size = "12",
            condition = "good",
            material = "Rayon",
            imageUrl = "https://example.com/feed-primary.webp",
            additionalImageUrls = new[] { "https://example.com/feed-extra-1.webp" },
            inStock = true,
            status = "live",
        });

        HttpClient anon = _factory.CreateClient();
        string xml = await (await anon.GetAsync("/api/merchant-feed.xml")).Content.ReadAsStringAsync();

        Assert.Contains("Feed Test Navy Dress", xml);
        Assert.Contains("feed-primary.webp", xml);
        Assert.Contains("feed-extra-1.webp", xml);
        Assert.Contains("<g:price>42.00 GBP</g:price>", xml);
        Assert.Contains("<g:condition>used</g:condition>", xml);
        Assert.Contains("<g:availability>in_stock</g:availability>", xml);
        Assert.Contains("<g:country>GB</g:country>", xml);
        // "Navy" in the title should be picked up as the colour attribute.
        Assert.Contains("<g:color>Navy</g:color>", xml);
        // HTML must be stripped from the description.
        Assert.DoesNotContain("<strong>", xml);
    }

    [Fact]
    public async Task Feed_ExcludesNonLiveProducts()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "admin-feed-stock@test.com");

        await client.PostAsJsonAsync("/api/products", new
        {
            name = "Feed Hidden Stock Item",
            description = "not yet listed",
            price = 30m,
            era = "1980s",
            category = "80s",
            size = "10",
            condition = "good",
            imageUrl = "https://example.com/feed-should-not-appear.webp",
            inStock = true,
            status = "stock",
        });

        HttpClient anon = _factory.CreateClient();
        string xml = await (await anon.GetAsync("/api/merchant-feed.xml")).Content.ReadAsStringAsync();

        Assert.DoesNotContain("feed-should-not-appear.webp", xml);
    }
}
