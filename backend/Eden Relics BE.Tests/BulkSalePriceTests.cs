using System.Net;
using System.Net.Http.Json;
using Eden_Relics_BE.Data;
using Eden_Relics_BE.Data.Entities;
using Microsoft.Extensions.DependencyInjection;
using static Eden_Relics_BE.Tests.Helpers;

namespace Eden_Relics_BE.Tests;

[Collection(SaleNotificationCollection.Name)]
public class BulkSalePriceTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public BulkSalePriceTests(ApiFactory factory)
    {
        _factory = factory;
    }

    private record BulkResult(int Updated, int Skipped, int Notified, List<BulkProduct> Products);
    private record BulkProduct(Guid Id, string Name, decimal Price, decimal? SalePrice);
    private record ErrorResponse(string Error);

    private static async Task<Guid> CreateProduct(HttpClient client, string name, decimal price)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/products", new
        {
            name,
            description = "Desc",
            price,
            era = "1990s",
            category = "90s",
            size = "10",
            condition = "good",
            imageUrl = "https://example.com/img.jpg",
            inStock = true
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        ProductResponse? created = await response.Content.ReadFromJsonAsync<ProductResponse>(JsonOptions);
        return created!.Id;
    }

    [Fact]
    public async Task BulkSale_AppliesPercentageOffEachProductsOwnPrice()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "admin-bulk-apply@test.com");

        Guid a = await CreateProduct(client, "Bulk A", 145m);
        Guid b = await CreateProduct(client, "Bulk B", 137m);

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/products/bulk-sale", new
        {
            productIds = new[] { a, b },
            discountPercent = 20m
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        BulkResult? result = await response.Content.ReadFromJsonAsync<BulkResult>(JsonOptions);

        Assert.NotNull(result);
        Assert.Equal(2, result.Updated);
        Assert.Equal(0, result.Skipped);
        Assert.Equal(0, result.Notified);
        Assert.Equal(116.00m, result.Products.Single(p => p.Id == a).SalePrice);
        // Rounded to the nearest penny, not to a round pound: 137 - 20% = 109.60.
        Assert.Equal(109.60m, result.Products.Single(p => p.Id == b).SalePrice);
    }

    [Fact]
    public async Task BulkSale_LeavesFullPriceAndItsHistoryAlone()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "admin-bulk-history@test.com");

        Guid id = await CreateProduct(client, "Bulk History", 200m);

        DateTime? priceSetBefore;
        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            EdenRelicsDbContext db = scope.ServiceProvider.GetRequiredService<EdenRelicsDbContext>();
            priceSetBefore = (await db.Products.FindAsync(id))!.PriceSetAtUtc;
        }

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/products/bulk-sale", new
        {
            productIds = new[] { id },
            discountPercent = 25m
        });
        response.EnsureSuccessStatusCode();

        using IServiceScope after = _factory.Services.CreateScope();
        EdenRelicsDbContext afterDb = after.ServiceProvider.GetRequiredService<EdenRelicsDbContext>();
        Product product = (await afterDb.Products.FindAsync(id))!;
        Assert.Equal(200m, product.Price);
        Assert.Equal(150m, product.SalePrice);
        Assert.Equal(priceSetBefore, product.PriceSetAtUtc);
        Assert.NotNull(product.SalePriceSetAtUtc);
    }

    [Fact]
    public async Task BulkSale_RunTwice_DoesNotCompoundTheDiscount()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "admin-bulk-compound@test.com");

        Guid id = await CreateProduct(client, "Bulk Compound", 100m);
        object payload = new { productIds = new[] { id }, discountPercent = 30m };

        HttpResponseMessage first = await client.PostAsJsonAsync("/api/products/bulk-sale", payload);
        first.EnsureSuccessStatusCode();
        HttpResponseMessage second = await client.PostAsJsonAsync("/api/products/bulk-sale", payload);
        BulkResult? result = await second.Content.ReadFromJsonAsync<BulkResult>(JsonOptions);

        Assert.NotNull(result);
        // Second run is a no-op: still 30% off the full price, and nothing reported as updated.
        Assert.Equal(0, result.Updated);
        Assert.Equal(70m, result.Products.Single().SalePrice);
    }

    [Fact]
    public async Task BulkSale_Clear_RestoresFullPrice()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "admin-bulk-clear@test.com");

        Guid id = await CreateProduct(client, "Bulk Clear", 80m);
        HttpResponseMessage applied = await client.PostAsJsonAsync("/api/products/bulk-sale", new
        {
            productIds = new[] { id },
            discountPercent = 10m
        });
        applied.EnsureSuccessStatusCode();

        HttpResponseMessage cleared = await client.PostAsJsonAsync("/api/products/bulk-sale/clear", new
        {
            productIds = new[] { id }
        });
        Assert.Equal(HttpStatusCode.OK, cleared.StatusCode);
        BulkResult? result = await cleared.Content.ReadFromJsonAsync<BulkResult>(JsonOptions);

        Assert.NotNull(result);
        Assert.Equal(1, result.Updated);
        Assert.Null(result.Products.Single().SalePrice);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(91)]
    public async Task BulkSale_DiscountOutsideOneToNinety_Returns400(decimal percent)
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, $"admin-bulk-pct-{percent}@test.com");

        Guid id = await CreateProduct(client, $"Bulk Pct {percent}", 100m);

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/products/bulk-sale", new
        {
            productIds = new[] { id },
            discountPercent = percent
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using IServiceScope scope = _factory.Services.CreateScope();
        EdenRelicsDbContext db = scope.ServiceProvider.GetRequiredService<EdenRelicsDbContext>();
        Assert.Null((await db.Products.FindAsync(id))!.SalePrice);
    }

    [Fact]
    public async Task BulkSale_EmptySelection_Returns400()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "admin-bulk-empty@test.com");

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/products/bulk-sale", new
        {
            productIds = Array.Empty<Guid>(),
            discountPercent = 20m
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        ErrorResponse? error = await response.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions);
        Assert.NotNull(error);
        Assert.Contains("at least one", error.Error);
    }

    [Fact]
    public async Task BulkSale_UnknownIds_CountAsSkipped()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "admin-bulk-unknown@test.com");

        Guid known = await CreateProduct(client, "Bulk Known", 60m);

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/products/bulk-sale", new
        {
            productIds = new[] { known, Guid.NewGuid() },
            discountPercent = 50m
        });
        response.EnsureSuccessStatusCode();
        BulkResult? result = await response.Content.ReadFromJsonAsync<BulkResult>(JsonOptions);

        Assert.NotNull(result);
        Assert.Equal(1, result.Updated);
        Assert.Equal(1, result.Skipped);
        Assert.Equal(30m, result.Products.Single().SalePrice);
    }

    [Fact]
    public async Task BulkSale_NotifyFavourites_ReportsNotifiedCount()
    {
        HttpClient adminClient = _factory.CreateClient();
        await RegisterAdmin(adminClient, _factory, "admin-bulk-notify@test.com");
        Guid id = await CreateProduct(adminClient, "Bulk Notify", 90m);

        HttpClient userClient = _factory.CreateClient();
        await RegisterAndLogin(userClient, "user-bulk-notify@test.com");
        HttpResponseMessage fav = await userClient.PostAsync($"/api/favourites/{id}", null);
        Assert.Equal(HttpStatusCode.Created, fav.StatusCode);

        HttpResponseMessage response = await adminClient.PostAsJsonAsync("/api/products/bulk-sale", new
        {
            productIds = new[] { id },
            discountPercent = 15m,
            notifyFavourites = true
        });
        response.EnsureSuccessStatusCode();
        BulkResult? result = await response.Content.ReadFromJsonAsync<BulkResult>(JsonOptions);

        Assert.NotNull(result);
        Assert.Equal(1, result.Updated);
        Assert.Equal(1, result.Notified);
        Assert.Equal(76.50m, result.Products.Single().SalePrice);
    }

    [Fact]
    public async Task BulkSale_WithoutAdminRole_Returns403()
    {
        HttpClient adminClient = _factory.CreateClient();
        await RegisterAdmin(adminClient, _factory, "admin-bulk-authz@test.com");
        Guid id = await CreateProduct(adminClient, "Bulk Authz", 100m);

        HttpClient userClient = _factory.CreateClient();
        await RegisterAndLogin(userClient, "user-bulk-authz@test.com");

        HttpResponseMessage response = await userClient.PostAsJsonAsync("/api/products/bulk-sale", new
        {
            productIds = new[] { id },
            discountPercent = 20m
        });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
