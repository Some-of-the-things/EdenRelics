using System.Net;
using System.Net.Http.Json;
using static Eden_Relics_BE.Tests.Helpers;

namespace Eden_Relics_BE.Tests;

public class ProductsTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public ProductsTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetAll_ReturnsSeededProducts()
    {
        var client = _factory.CreateClient();
        var products = await client.GetFromJsonAsync<List<ProductResponse>>("/api/products", JsonOptions);
        Assert.NotNull(products);
        Assert.True(products.Count >= 10, $"Expected at least 10 seeded products, got {products.Count}");
    }

    [Fact]
    public async Task GetById_ReturnsProduct()
    {
        var client = _factory.CreateClient();
        var id = Guid.Parse("a1b2c3d4-0001-0000-0000-000000000001");
        var product = await client.GetFromJsonAsync<ProductResponse>($"/api/products/{id}", JsonOptions);
        Assert.NotNull(product);
        Assert.Equal("Bohemian Maxi Dress", product.Name);
        Assert.Equal(195m, product.Price);
    }

    [Fact]
    public async Task GetById_NonExistent_Returns404()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync($"/api/products/{Guid.Empty}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetByCategory_FiltersCorrectly()
    {
        var client = _factory.CreateClient();
        var products = await client.GetFromJsonAsync<List<ProductResponse>>("/api/products/category/70s", JsonOptions);
        Assert.NotNull(products);
        Assert.Equal(2, products.Count);
        Assert.All(products, p => Assert.Equal("70s", p.Category));
    }

    [Fact]
    public async Task GetByCategory_Empty_ReturnsEmptyList()
    {
        var client = _factory.CreateClient();
        var products = await client.GetFromJsonAsync<List<ProductResponse>>("/api/products/category/nonexistent", JsonOptions);
        Assert.NotNull(products);
        Assert.Empty(products);
    }

    [Fact]
    public async Task Create_AsAdmin_ReturnsCreatedProduct()
    {
        var client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "admin-create@test.com");

        var response = await client.PostAsJsonAsync("/api/products", new
        {
            name = "Test Dress",
            description = "A test product",
            price = 99.99m,
            era = "1990s",
            category = "90s",
            size = "M",
            condition = "good",
            imageUrl = "https://example.com/img.jpg",
            inStock = true
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var product = await response.Content.ReadFromJsonAsync<ProductResponse>(JsonOptions);
        Assert.NotNull(product);
        Assert.Equal("Test Dress", product.Name);
        Assert.Equal(99.99m, product.Price);
    }

    [Fact]
    public async Task Create_Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/products", new
        {
            name = "Hack",
            description = "Desc",
            price = 1m,
            era = "1990s",
            category = "90s",
            size = "M",
            condition = "good",
            imageUrl = "https://example.com/img.jpg",
            inStock = true
        });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Create_AsCustomer_Returns403()
    {
        var client = _factory.CreateClient();
        await RegisterAndLogin(client, "customer-create@test.com");

        var response = await client.PostAsJsonAsync("/api/products", new
        {
            name = "Hack",
            description = "Desc",
            price = 1m,
            era = "1990s",
            category = "90s",
            size = "M",
            condition = "good",
            imageUrl = "https://example.com/img.jpg",
            inStock = true
        });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Update_AsAdmin_ModifiesProduct()
    {
        var client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "admin-update@test.com");

        var createResponse = await client.PostAsJsonAsync("/api/products", new
        {
            name = "Original",
            description = "Desc",
            price = 50m,
            era = "1980s",
            category = "80s",
            size = "S",
            condition = "good",
            imageUrl = "https://example.com/img.jpg",
            inStock = true
        });
        var created = await createResponse.Content.ReadFromJsonAsync<ProductResponse>(JsonOptions);

        var updateResponse = await client.PutAsJsonAsync($"/api/products/{created!.Id}", new
        {
            name = "Updated Name",
            price = 75m
        });
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = await updateResponse.Content.ReadFromJsonAsync<ProductResponse>(JsonOptions);
        Assert.Equal("Updated Name", updated!.Name);
        Assert.Equal(75m, updated.Price);
        Assert.Equal("Desc", updated.Description);
    }

    [Fact]
    public async Task Update_Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient();
        var id = Guid.Parse("a1b2c3d4-0001-0000-0000-000000000001");
        var response = await client.PutAsJsonAsync($"/api/products/{id}", new { name = "Hacked" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Update_AsCustomer_Returns403()
    {
        var client = _factory.CreateClient();
        await RegisterAndLogin(client, "customer-update@test.com");

        var id = Guid.Parse("a1b2c3d4-0001-0000-0000-000000000001");
        var response = await client.PutAsJsonAsync($"/api/products/{id}", new { name = "Hacked" });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Delete_AsAdmin_RemovesProduct()
    {
        var client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "admin-delete@test.com");

        var createResponse = await client.PostAsJsonAsync("/api/products", new
        {
            name = "To Delete",
            description = "Desc",
            price = 10m,
            era = "1990s",
            category = "90s",
            size = "M",
            condition = "good",
            imageUrl = "https://example.com/img.jpg",
            inStock = true
        });
        var created = await createResponse.Content.ReadFromJsonAsync<ProductResponse>(JsonOptions);

        var deleteResponse = await client.DeleteAsync($"/api/products/{created!.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var getResponse = await client.GetAsync($"/api/products/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task Delete_Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient();
        var id = Guid.Parse("a1b2c3d4-0001-0000-0000-000000000001");
        var response = await client.DeleteAsync($"/api/products/{id}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Delete_AsCustomer_Returns403()
    {
        var client = _factory.CreateClient();
        await RegisterAndLogin(client, "customer-delete@test.com");

        var id = Guid.Parse("a1b2c3d4-0001-0000-0000-000000000001");
        var response = await client.DeleteAsync($"/api/products/{id}");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
