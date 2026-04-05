using System.Net;
using System.Net.Http.Json;
using static Eden_Relics_BE.Tests.Helpers;

namespace Eden_Relics_BE.Tests;

// Orders tests that don't involve Stripe (the Create endpoint calls Stripe API,
// so we test the validation paths and the read endpoints).
public class OrdersTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    private static readonly Guid SeededProductId = Guid.Parse("a1b2c3d4-0001-0000-0000-000000000001");

    public OrdersTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Create_NoEmailNoAuth_Returns400()
    {
        HttpClient client = _factory.CreateClient();
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/orders", new
        {
            items = new[] { new { productId = SeededProductId, quantity = 1 } }
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_InvalidProduct_Returns400()
    {
        HttpClient client = _factory.CreateClient();
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/orders", new
        {
            items = new[] { new { productId = Guid.Empty, quantity = 1 } },
            guestEmail = "guest@test.com"
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetMyOrders_Unauthenticated_Returns401()
    {
        HttpClient client = _factory.CreateClient();
        HttpResponseMessage response = await client.GetAsync("/api/orders");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetMyOrders_Authenticated_ReturnsEmptyInitially()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAndLogin(client, "orders@test.com");

        List<OrderResponse>? orders = await client.GetFromJsonAsync<List<OrderResponse>>("/api/orders", JsonOptions);
        Assert.NotNull(orders);
        Assert.Empty(orders);
    }

    [Fact]
    public async Task GetById_NonExistent_Returns404()
    {
        HttpClient client = _factory.CreateClient();
        HttpResponseMessage response = await client.GetAsync($"/api/orders/{Guid.Empty}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
