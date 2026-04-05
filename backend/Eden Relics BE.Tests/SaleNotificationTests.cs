using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using static Eden_Relics_BE.Tests.Helpers;

namespace Eden_Relics_BE.Tests;

public class SaleNotificationTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public SaleNotificationTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task UpdateProduct_SetSalePrice_WithFavourites_Returns200()
    {
        // 1. Admin creates a product
        HttpClient adminClient = _factory.CreateClient();
        await RegisterAdmin(adminClient, _factory, "admin-notify-fav@test.com");

        HttpResponseMessage createResponse = await adminClient.PostAsJsonAsync("/api/products", new
        {
            name = "Notify Fav Product",
            description = "Desc",
            price = 100m,
            era = "1990s",
            category = "90s",
            size = "M",
            condition = "good",
            imageUrl = "https://example.com/img.jpg",
            inStock = true
        });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        ProductResponse? created = await createResponse.Content.ReadFromJsonAsync<ProductResponse>(JsonOptions);

        // 2. A user favourites the product
        HttpClient userClient = _factory.CreateClient();
        await RegisterAndLogin(userClient, "user-notify-fav@test.com");
        HttpResponseMessage favResponse = await userClient.PostAsync($"/api/favourites/{created!.Id}", null);
        Assert.Equal(HttpStatusCode.Created, favResponse.StatusCode);

        // 3. Admin sets a sale price (triggers fire-and-forget notification)
        HttpResponseMessage updateResponse = await adminClient.PutAsJsonAsync($"/api/products/{created.Id}", new
        {
            salePrice = 79.99m
        });
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        ProductResponse? updated = await updateResponse.Content.ReadFromJsonAsync<ProductResponse>(JsonOptions);
        Assert.Equal(79.99m, updated!.SalePrice);
    }

    [Fact]
    public async Task UpdateProduct_SetSalePrice_NoFavourites_Returns200()
    {
        // Admin creates a product and sets sale price with no favourites
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "admin-notify-nofav@test.com");

        HttpResponseMessage createResponse = await client.PostAsJsonAsync("/api/products", new
        {
            name = "No Fav Sale Product",
            description = "Desc",
            price = 100m,
            era = "1990s",
            category = "90s",
            size = "M",
            condition = "good",
            imageUrl = "https://example.com/img.jpg",
            inStock = true
        });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        ProductResponse? created = await createResponse.Content.ReadFromJsonAsync<ProductResponse>(JsonOptions);

        HttpResponseMessage updateResponse = await client.PutAsJsonAsync($"/api/products/{created!.Id}", new
        {
            salePrice = 69.99m
        });
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        ProductResponse? updated = await updateResponse.Content.ReadFromJsonAsync<ProductResponse>(JsonOptions);
        Assert.Equal(69.99m, updated!.SalePrice);
    }
}
