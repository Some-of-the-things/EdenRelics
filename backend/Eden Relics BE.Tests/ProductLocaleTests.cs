using System.Net;
using System.Net.Http.Json;
using static Eden_Relics_BE.Tests.Helpers;

namespace Eden_Relics_BE.Tests;

public class ProductLocaleTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public ProductLocaleTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetAll_WithoutLocale_ReturnsEnglishNames()
    {
        HttpClient client = _factory.CreateClient();
        List<ProductResponse>? products = await client.GetFromJsonAsync<List<ProductResponse>>("/api/products", JsonOptions);
        Assert.NotNull(products);
        Assert.Contains(products, p => p.Name == "Bohemian Maxi Dress");
    }

    [Fact]
    public async Task GetAll_WithEnLocale_ReturnsEnglishNames()
    {
        HttpClient client = _factory.CreateClient();
        List<ProductResponse>? products = await client.GetFromJsonAsync<List<ProductResponse>>("/api/products?locale=en", JsonOptions);
        Assert.NotNull(products);
        Assert.Contains(products, p => p.Name == "Bohemian Maxi Dress");
    }

    [Fact]
    public async Task GetAll_WithUnsupportedLocale_ReturnsEnglishFallback()
    {
        HttpClient client = _factory.CreateClient();
        List<ProductResponse>? products = await client.GetFromJsonAsync<List<ProductResponse>>("/api/products?locale=xx", JsonOptions);
        Assert.NotNull(products);
        // Should fall back to English
        Assert.Contains(products, p => p.Name == "Bohemian Maxi Dress");
    }

    [Fact]
    public async Task GetById_WithoutLocale_ReturnsEnglish()
    {
        HttpClient client = _factory.CreateClient();
        string id = "a1b2c3d4-0001-0000-0000-000000000001";
        ProductResponse? product = await client.GetFromJsonAsync<ProductResponse>($"/api/products/{id}", JsonOptions);
        Assert.NotNull(product);
        Assert.Equal("Bohemian Maxi Dress", product.Name);
    }

    [Fact]
    public async Task GetById_WithLocale_FallsBackToEnglishWhenNoTranslation()
    {
        HttpClient client = _factory.CreateClient();
        string id = "a1b2c3d4-0001-0000-0000-000000000001";
        ProductResponse? product = await client.GetFromJsonAsync<ProductResponse>($"/api/products/{id}?locale=fr", JsonOptions);
        Assert.NotNull(product);
        // Seeded products have no translations — should fall back to English
        Assert.Equal("Bohemian Maxi Dress", product.Name);
    }

    [Fact]
    public async Task GetByCategory_WithLocale_ReturnsProducts()
    {
        HttpClient client = _factory.CreateClient();
        List<ProductResponse>? products = await client.GetFromJsonAsync<List<ProductResponse>>("/api/products/category/70s?locale=fr", JsonOptions);
        Assert.NotNull(products);
        Assert.Equal(2, products.Count);
    }

    [Fact]
    public async Task GetAll_AdminEndpoint_IgnoresLocale()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "product-locale-admin@test.com");

        // Admin should always see English (locale param ignored for admin DTOs)
        List<ProductResponse>? products = await client.GetFromJsonAsync<List<ProductResponse>>("/api/products?locale=fr", JsonOptions);
        Assert.NotNull(products);
        // Admin sees original English names
        Assert.Contains(products, p => p.Name == "Bohemian Maxi Dress");
    }

    [Fact]
    public async Task Create_Product_StoresEnglishName()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "product-locale-create@test.com");

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/products", new
        {
            name = "Vintage Silk Blouse",
            description = "A beautiful vintage silk blouse from the 1980s",
            price = 85m,
            era = "1980s",
            category = "80s",
            size = "10",
            condition = "excellent",
            imageUrl = "https://example.com/img.webp",
            inStock = true
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        ProductResponse? product = await response.Content.ReadFromJsonAsync<ProductResponse>(JsonOptions);
        Assert.NotNull(product);
        Assert.Equal("Vintage Silk Blouse", product.Name);

        // Verify it's returned in a locale-less GET
        ProductResponse? fetched = await client.GetFromJsonAsync<ProductResponse>($"/api/products/{product.Id}", JsonOptions);
        Assert.Equal("Vintage Silk Blouse", fetched!.Name);
    }
}
