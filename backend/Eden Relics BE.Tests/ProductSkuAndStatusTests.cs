using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Eden_Relics_BE.Data;
using Eden_Relics_BE.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Eden_Relics_BE.Tests.Helpers;

namespace Eden_Relics_BE.Tests;

public class ProductSkuAndStatusTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public ProductSkuAndStatusTests(ApiFactory factory)
    {
        _factory = factory;
    }

    private record AdminProductResponse(
        Guid Id,
        string Name,
        string Slug,
        string Sku,
        string Description,
        decimal Price,
        decimal? SalePrice,
        decimal CostPrice,
        string? Supplier,
        string Era,
        string Category,
        string Size,
        string Condition,
        string ImageUrl,
        List<string> AdditionalImageUrls,
        List<string> VideoUrls,
        bool InStock,
        string Status,
        int ViewCount
    );

    private record PublicProductResponse(
        Guid Id,
        string Name,
        string Slug,
        string Description,
        decimal Price,
        decimal? SalePrice,
        bool ShowReduction,
        int DiscountPercent,
        string Era,
        string Category,
        string Size,
        string Condition,
        string ImageUrl,
        List<string> AdditionalImageUrls,
        List<string> VideoUrls,
        bool InStock
    );

    private static object NewProductPayload(
        string name = "Test Product",
        string? sku = null,
        string? status = null,
        bool inStock = true)
    {
        Dictionary<string, object?> payload = new()
        {
            ["name"] = name,
            ["description"] = "A test product",
            ["price"] = 99m,
            ["era"] = "1990s",
            ["category"] = "90s",
            ["size"] = "10",
            ["condition"] = "good",
            ["imageUrl"] = "https://example.com/img.jpg",
            ["inStock"] = inStock,
        };
        if (sku is not null) { payload["sku"] = sku; }
        if (status is not null) { payload["status"] = status; }
        return payload;
    }

    [Fact]
    public async Task Create_WithoutSku_AutoGeneratesSequentialSku()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "admin-sku-auto@test.com");

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/products", NewProductPayload());
        response.EnsureSuccessStatusCode();
        AdminProductResponse? product = await response.Content.ReadFromJsonAsync<AdminProductResponse>(JsonOptions);

        Assert.NotNull(product);
        Assert.StartsWith("ER-", product.Sku);
        Assert.True(product.Sku.Length >= 6, $"Auto SKU '{product.Sku}' looks malformed");
    }

    [Fact]
    public async Task Create_TwoProductsInSequence_GetIncrementingSkus()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "admin-sku-seq@test.com");

        HttpResponseMessage r1 = await client.PostAsJsonAsync("/api/products", NewProductPayload(name: "First"));
        AdminProductResponse? p1 = await r1.Content.ReadFromJsonAsync<AdminProductResponse>(JsonOptions);
        HttpResponseMessage r2 = await client.PostAsJsonAsync("/api/products", NewProductPayload(name: "Second"));
        AdminProductResponse? p2 = await r2.Content.ReadFromJsonAsync<AdminProductResponse>(JsonOptions);

        Assert.NotNull(p1);
        Assert.NotNull(p2);

        int seq1 = int.Parse(p1.Sku.AsSpan(3));
        int seq2 = int.Parse(p2.Sku.AsSpan(3));
        Assert.Equal(seq1 + 1, seq2);
    }

    [Fact]
    public async Task Create_WithManualSku_StoresAsProvided()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "admin-sku-manual@test.com");

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/products",
            NewProductPayload(sku: "CUSTOM-A-1"));
        response.EnsureSuccessStatusCode();
        AdminProductResponse? product = await response.Content.ReadFromJsonAsync<AdminProductResponse>(JsonOptions);

        Assert.NotNull(product);
        Assert.Equal("CUSTOM-A-1", product.Sku);
    }

    [Fact]
    public async Task Create_WithDuplicateManualSku_Returns400()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "admin-sku-dupe@test.com");

        HttpResponseMessage first = await client.PostAsJsonAsync(
            "/api/products",
            NewProductPayload(name: "First", sku: "DUPE-1"));
        first.EnsureSuccessStatusCode();

        HttpResponseMessage second = await client.PostAsJsonAsync(
            "/api/products",
            NewProductPayload(name: "Second", sku: "DUPE-1"));
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
    }

    [Fact]
    public async Task Create_WithEmptyStringSku_AutoGenerates()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "admin-sku-empty@test.com");

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/products",
            NewProductPayload(sku: "   "));
        response.EnsureSuccessStatusCode();
        AdminProductResponse? product = await response.Content.ReadFromJsonAsync<AdminProductResponse>(JsonOptions);

        Assert.NotNull(product);
        Assert.StartsWith("ER-", product.Sku);
    }

    [Fact]
    public async Task Update_ChangeSkuToUnique_Succeeds()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "admin-sku-update@test.com");

        HttpResponseMessage create = await client.PostAsJsonAsync("/api/products", NewProductPayload());
        AdminProductResponse? created = await create.Content.ReadFromJsonAsync<AdminProductResponse>(JsonOptions);

        HttpResponseMessage update = await client.PutAsJsonAsync($"/api/products/{created!.Id}", new
        {
            sku = "RENAMED-1"
        });
        update.EnsureSuccessStatusCode();

        AdminProductResponse? reloaded = await client.GetFromJsonAsync<AdminProductResponse>(
            $"/api/products/{created.Id}", JsonOptions);
        Assert.NotNull(reloaded);
        Assert.Equal("RENAMED-1", reloaded.Sku);
    }

    [Fact]
    public async Task Update_ChangeSkuToDuplicate_Returns400()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "admin-sku-update-dupe@test.com");

        HttpResponseMessage a = await client.PostAsJsonAsync(
            "/api/products",
            NewProductPayload(name: "A", sku: "AAA-1"));
        AdminProductResponse? prodA = await a.Content.ReadFromJsonAsync<AdminProductResponse>(JsonOptions);
        HttpResponseMessage b = await client.PostAsJsonAsync(
            "/api/products",
            NewProductPayload(name: "B", sku: "BBB-1"));
        AdminProductResponse? prodB = await b.Content.ReadFromJsonAsync<AdminProductResponse>(JsonOptions);

        HttpResponseMessage update = await client.PutAsJsonAsync($"/api/products/{prodB!.Id}", new
        {
            sku = "AAA-1"
        });
        Assert.Equal(HttpStatusCode.BadRequest, update.StatusCode);
    }

    [Fact]
    public async Task Create_WithStatusStock_HidesFromPublicList()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "admin-stock-public@test.com");

        HttpResponseMessage create = await client.PostAsJsonAsync(
            "/api/products",
            NewProductPayload(name: "Secret Stock", status: "stock"));
        create.EnsureSuccessStatusCode();
        AdminProductResponse? stockProduct = await create.Content.ReadFromJsonAsync<AdminProductResponse>(JsonOptions);

        // Switch to anonymous client
        HttpClient anon = _factory.CreateClient();
        List<PublicProductResponse>? publicList = await anon.GetFromJsonAsync<List<PublicProductResponse>>(
            "/api/products", JsonOptions);
        Assert.NotNull(publicList);
        Assert.DoesNotContain(publicList, p => p.Id == stockProduct!.Id);
    }

    [Fact]
    public async Task GetById_StockProduct_AsAnonymous_Returns404()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "admin-stock-byid@test.com");

        HttpResponseMessage create = await client.PostAsJsonAsync(
            "/api/products",
            NewProductPayload(status: "stock"));
        AdminProductResponse? stockProduct = await create.Content.ReadFromJsonAsync<AdminProductResponse>(JsonOptions);

        HttpClient anon = _factory.CreateClient();
        HttpResponseMessage response = await anon.GetAsync($"/api/products/{stockProduct!.Id}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetBySlug_StockProduct_AsAnonymous_Returns404()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "admin-stock-slug@test.com");

        HttpResponseMessage create = await client.PostAsJsonAsync(
            "/api/products",
            NewProductPayload(name: "Stock Only Item", status: "stock"));
        AdminProductResponse? stockProduct = await create.Content.ReadFromJsonAsync<AdminProductResponse>(JsonOptions);

        HttpClient anon = _factory.CreateClient();
        HttpResponseMessage response = await anon.GetAsync($"/api/products/by-slug/{stockProduct!.Slug}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetByCategory_StockProduct_NotInList_AsAnonymous()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "admin-stock-cat@test.com");

        HttpResponseMessage create = await client.PostAsJsonAsync(
            "/api/products",
            NewProductPayload(status: "stock"));
        AdminProductResponse? stockProduct = await create.Content.ReadFromJsonAsync<AdminProductResponse>(JsonOptions);

        HttpClient anon = _factory.CreateClient();
        List<PublicProductResponse>? products = await anon.GetFromJsonAsync<List<PublicProductResponse>>(
            "/api/products/category/90s", JsonOptions);
        Assert.NotNull(products);
        Assert.DoesNotContain(products, p => p.Id == stockProduct!.Id);
    }

    [Fact]
    public async Task GetAll_AsAdmin_IncludesStockProducts()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "admin-stock-admin-list@test.com");

        HttpResponseMessage create = await client.PostAsJsonAsync(
            "/api/products",
            NewProductPayload(status: "stock"));
        AdminProductResponse? stockProduct = await create.Content.ReadFromJsonAsync<AdminProductResponse>(JsonOptions);

        List<AdminProductResponse>? adminList = await client.GetFromJsonAsync<List<AdminProductResponse>>(
            "/api/products", JsonOptions);
        Assert.NotNull(adminList);
        Assert.Contains(adminList, p => p.Id == stockProduct!.Id && p.Status == "stock");
    }

    [Fact]
    public async Task GetById_StockProduct_AsAdmin_Returns200()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "admin-stock-admin-byid@test.com");

        HttpResponseMessage create = await client.PostAsJsonAsync(
            "/api/products",
            NewProductPayload(status: "stock"));
        AdminProductResponse? stockProduct = await create.Content.ReadFromJsonAsync<AdminProductResponse>(JsonOptions);

        HttpResponseMessage detail = await client.GetAsync($"/api/products/{stockProduct!.Id}");
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
    }

    [Fact]
    public async Task RecordView_OnStockProduct_AsAnonymous_Returns404()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "admin-stock-view@test.com");

        HttpResponseMessage create = await client.PostAsJsonAsync(
            "/api/products",
            NewProductPayload(status: "stock"));
        AdminProductResponse? stockProduct = await create.Content.ReadFromJsonAsync<AdminProductResponse>(JsonOptions);

        HttpClient anon = _factory.CreateClient();
        HttpResponseMessage response = await anon.PostAsJsonAsync(
            $"/api/products/{stockProduct!.Id}/view",
            new { });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CartInterest_OnStockProduct_AsAnonymous_Returns404()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "admin-stock-cart@test.com");

        HttpResponseMessage create = await client.PostAsJsonAsync(
            "/api/products",
            NewProductPayload(status: "stock"));
        AdminProductResponse? stockProduct = await create.Content.ReadFromJsonAsync<AdminProductResponse>(JsonOptions);

        HttpClient anon = _factory.CreateClient();
        HttpResponseMessage getResponse = await anon.GetAsync($"/api/products/{stockProduct!.Id}/cart-interest");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);

        HttpResponseMessage postResponse = await anon.PostAsJsonAsync(
            $"/api/products/{stockProduct.Id}/cart-interest",
            new { sessionId = "abc" });
        Assert.Equal(HttpStatusCode.NotFound, postResponse.StatusCode);
    }

    [Fact]
    public async Task Sitemap_ExcludesStockProducts()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "admin-stock-sitemap@test.com");

        HttpResponseMessage create = await client.PostAsJsonAsync(
            "/api/products",
            NewProductPayload(name: "Stealthy Sweater", status: "stock"));
        AdminProductResponse? stockProduct = await create.Content.ReadFromJsonAsync<AdminProductResponse>(JsonOptions);

        HttpClient anon = _factory.CreateClient();
        HttpResponseMessage sitemap = await anon.GetAsync("/api/sitemap.xml");
        sitemap.EnsureSuccessStatusCode();
        string xml = await sitemap.Content.ReadAsStringAsync();

        Assert.DoesNotContain($"/product/{stockProduct!.Slug}", xml);
        Assert.DoesNotContain($"/product/{stockProduct.Id}", xml);
    }

    [Fact]
    public async Task UpdateStatus_StockToLive_BecomesVisible()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "admin-stock-promote@test.com");

        HttpResponseMessage create = await client.PostAsJsonAsync(
            "/api/products",
            NewProductPayload(status: "stock"));
        AdminProductResponse? p = await create.Content.ReadFromJsonAsync<AdminProductResponse>(JsonOptions);

        HttpClient anon = _factory.CreateClient();
        HttpResponseMessage hiddenResponse = await anon.GetAsync($"/api/products/{p!.Id}");
        Assert.Equal(HttpStatusCode.NotFound, hiddenResponse.StatusCode);

        HttpResponseMessage promote = await client.PutAsJsonAsync($"/api/products/{p.Id}", new { status = "live" });
        promote.EnsureSuccessStatusCode();

        HttpResponseMessage visibleResponse = await anon.GetAsync($"/api/products/{p.Id}");
        Assert.Equal(HttpStatusCode.OK, visibleResponse.StatusCode);
    }

    [Fact]
    public async Task UpdateStatus_LiveToStock_Hides()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "admin-stock-demote@test.com");

        HttpResponseMessage create = await client.PostAsJsonAsync(
            "/api/products",
            NewProductPayload(status: "live"));
        AdminProductResponse? p = await create.Content.ReadFromJsonAsync<AdminProductResponse>(JsonOptions);

        HttpClient anon = _factory.CreateClient();
        HttpResponseMessage visibleResponse = await anon.GetAsync($"/api/products/{p!.Id}");
        Assert.Equal(HttpStatusCode.OK, visibleResponse.StatusCode);

        HttpResponseMessage demote = await client.PutAsJsonAsync($"/api/products/{p.Id}", new { status = "stock" });
        demote.EnsureSuccessStatusCode();

        HttpResponseMessage hiddenResponse = await anon.GetAsync($"/api/products/{p.Id}");
        Assert.Equal(HttpStatusCode.NotFound, hiddenResponse.StatusCode);
    }

    [Fact]
    public async Task SoldProduct_StaysViewableDuringGrace_ThenHidden_AlwaysVisibleToAdmin()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "admin-sold-hidden@test.com");

        HttpResponseMessage create = await client.PostAsJsonAsync(
            "/api/products",
            NewProductPayload(status: "sold"));
        AdminProductResponse? p = await create.Content.ReadFromJsonAsync<AdminProductResponse>(JsonOptions);
        Assert.NotNull(p);

        HttpClient anon = _factory.CreateClient();

        // Within the 60-day grace window a just-sold piece stays viewable, so its
        // URL doesn't dead-end the moment it sells.
        HttpResponseMessage duringGrace = await anon.GetAsync($"/api/products/{p!.Id}");
        Assert.Equal(HttpStatusCode.OK, duringGrace.StatusCode);

        // Backdate the sold date past the grace window.
        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            EdenRelicsDbContext db = scope.ServiceProvider.GetRequiredService<EdenRelicsDbContext>();
            Product? row = await db.Products.FindAsync(p.Id);
            Assert.NotNull(row);
            row!.SoldAtUtc = DateTime.UtcNow.AddDays(-61);
            await db.SaveChangesAsync();
        }

        // Now the public page is hidden (vintage = one-of-one; long-gone is gone)...
        HttpResponseMessage afterGrace = await anon.GetAsync($"/api/products/{p.Id}");
        Assert.Equal(HttpStatusCode.NotFound, afterGrace.StatusCode);

        // ...and the resolve endpoint tells the edge layer to 301 it away.
        string resolved = await anon.GetStringAsync($"/api/products/resolve/{p.Slug}");
        Assert.Contains("redirect", resolved);

        // Admin can always see sold products for bookkeeping/management.
        HttpResponseMessage adminResponse = await client.GetAsync($"/api/products/{p.Id}");
        Assert.Equal(HttpStatusCode.OK, adminResponse.StatusCode);
    }

    [Fact]
    public async Task PublicDto_DoesNotLeakSkuOrStatus()
    {
        HttpClient anon = _factory.CreateClient();
        HttpResponseMessage response = await anon.GetAsync("/api/products");
        response.EnsureSuccessStatusCode();
        string body = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("\"sku\"", body);
        Assert.DoesNotContain("\"status\"", body);
    }

    [Fact]
    public async Task SeededProducts_HaveSequentialSkus()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        EdenRelicsDbContext db = scope.ServiceProvider.GetRequiredService<EdenRelicsDbContext>();

        Product? p1 = await db.Products.FindAsync(Guid.Parse("a1b2c3d4-0001-0000-0000-000000000001"));
        Product? p10 = await db.Products.FindAsync(Guid.Parse("a1b2c3d4-0010-0000-0000-000000000010"));

        Assert.NotNull(p1);
        Assert.NotNull(p10);
        Assert.Equal("ER-00001", p1.Sku);
        Assert.Equal("ER-00010", p10.Sku);
        Assert.Equal(ProductStatus.Live, p1.Status);
        Assert.Equal(ProductStatus.Live, p10.Status);
    }

    [Fact]
    public async Task LegacyInStockTrue_OnUpdate_BecomesLive()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "admin-legacy-true@test.com");

        HttpResponseMessage create = await client.PostAsJsonAsync(
            "/api/products",
            NewProductPayload(status: "sold"));
        AdminProductResponse? p = await create.Content.ReadFromJsonAsync<AdminProductResponse>(JsonOptions);

        HttpResponseMessage update = await client.PutAsJsonAsync($"/api/products/{p!.Id}", new { inStock = true });
        update.EnsureSuccessStatusCode();
        AdminProductResponse? reloaded = await client.GetFromJsonAsync<AdminProductResponse>(
            $"/api/products/{p.Id}", JsonOptions);
        Assert.NotNull(reloaded);
        Assert.Equal("live", reloaded.Status);
    }

    [Fact]
    public async Task LegacyInStockFalse_OnUpdate_BecomesSold()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "admin-legacy-false@test.com");

        HttpResponseMessage create = await client.PostAsJsonAsync(
            "/api/products",
            NewProductPayload(status: "live"));
        AdminProductResponse? p = await create.Content.ReadFromJsonAsync<AdminProductResponse>(JsonOptions);

        HttpResponseMessage update = await client.PutAsJsonAsync($"/api/products/{p!.Id}", new { inStock = false });
        update.EnsureSuccessStatusCode();
        AdminProductResponse? reloaded = await client.GetFromJsonAsync<AdminProductResponse>(
            $"/api/products/{p.Id}", JsonOptions);
        Assert.NotNull(reloaded);
        Assert.Equal("sold", reloaded.Status);
    }

    [Fact]
    public async Task OrderCheckout_StockProduct_Returns400()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "admin-order-stock@test.com");

        HttpResponseMessage create = await client.PostAsJsonAsync(
            "/api/products",
            NewProductPayload(status: "stock"));
        AdminProductResponse? p = await create.Content.ReadFromJsonAsync<AdminProductResponse>(JsonOptions);

        // Customer attempts to order a stock product
        HttpClient customer = _factory.CreateClient();
        await RegisterAndLogin(customer, "customer-order-stock@test.com");

        HttpResponseMessage order = await customer.PostAsJsonAsync("/api/orders", new
        {
            items = new[] { new { productId = p!.Id, quantity = 1 } },
            shippingMethod = "Standard",
        });
        Assert.Equal(HttpStatusCode.BadRequest, order.StatusCode);
    }

    [Fact]
    public async Task AccountsSummary_SeparatesStockFromLive()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "admin-accounts-stock@test.com");

        await client.PostAsJsonAsync("/api/products", NewProductPayload(name: "Stock A", status: "stock"));
        await client.PostAsJsonAsync("/api/products", NewProductPayload(name: "Stock B", status: "stock"));
        await client.PostAsJsonAsync("/api/products", NewProductPayload(name: "Live X", status: "live"));

        HttpResponseMessage response = await client.GetAsync("/api/accounts/summary");
        response.EnsureSuccessStatusCode();
        JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement inventory = doc.RootElement.GetProperty("inventory");

        int stock = inventory.GetProperty("stock").GetInt32();
        int live = inventory.GetProperty("inStock").GetInt32();

        Assert.True(stock >= 2, $"Expected at least 2 stock items, saw {stock}");
        Assert.True(live >= 1, $"Expected at least 1 live item, saw {live}");
    }
}
