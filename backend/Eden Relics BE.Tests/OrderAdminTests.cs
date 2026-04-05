using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Eden_Relics_BE.Data;
using Eden_Relics_BE.Data.Entities;
using Microsoft.Extensions.DependencyInjection;
using static Eden_Relics_BE.Tests.Helpers;

namespace Eden_Relics_BE.Tests;

public class OrderAdminTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public OrderAdminTests(ApiFactory factory)
    {
        _factory = factory;
    }

    private async Task<Guid> SeedOrder(string userEmail)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        EdenRelicsDbContext db = scope.ServiceProvider.GetRequiredService<EdenRelicsDbContext>();
        User? user = await db.Users.FindAsync(
            db.Users.First(u => u.Email == userEmail).Id);

        Order order = new()
        {
            UserId = user!.Id,
            Status = "Paid",
            Total = 195m,
            ShippingMethod = "standard",
            ShippingCost = 3.95m,
            Items =
            [
                new OrderItem
                {
                    ProductId = Guid.Parse("a1b2c3d4-0001-0000-0000-000000000001"),
                    ProductName = "Bohemian Maxi Dress",
                    UnitPrice = 195m,
                    Quantity = 1
                }
            ]
        };
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        return order.Id;
    }

    [Fact]
    public async Task GetAllOrders_AsAdmin_ReturnsOrders()
    {
        HttpClient client = _factory.CreateClient();
        var (_, auth) = await RegisterAdmin(client, _factory, "order-admin-getall@test.com");
        Guid orderId = await SeedOrder(auth.User.Email);

        HttpResponseMessage response = await client.GetAsync("/api/orders/admin/all");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        string json = await response.Content.ReadAsStringAsync();
        JsonElement orders = JsonDocument.Parse(json).RootElement;
        Assert.True(orders.GetArrayLength() >= 1);

        // Verify admin fields are present
        JsonElement firstOrder = orders[0];
        Assert.True(firstOrder.TryGetProperty("customerEmail", out _));
        Assert.True(firstOrder.TryGetProperty("customerName", out _));
    }

    [Fact]
    public async Task GetAllOrders_Unauthenticated_Returns401()
    {
        HttpClient client = _factory.CreateClient();
        HttpResponseMessage response = await client.GetAsync("/api/orders/admin/all");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetAllOrders_AsCustomer_Returns403()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAndLogin(client, "order-admin-customer@test.com");
        HttpResponseMessage response = await client.GetAsync("/api/orders/admin/all");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UpdateStatus_AsAdmin_ChangesStatus()
    {
        HttpClient client = _factory.CreateClient();
        var (_, auth) = await RegisterAdmin(client, _factory, "order-admin-status@test.com");
        Guid orderId = await SeedOrder(auth.User.Email);

        HttpResponseMessage response = await client.PutAsJsonAsync($"/api/orders/admin/{orderId}/status", new
        {
            status = "Shipped"
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        string json = await response.Content.ReadAsStringAsync();
        JsonElement order = JsonDocument.Parse(json).RootElement;
        Assert.Equal("Shipped", order.GetProperty("status").GetString());
    }

    [Fact]
    public async Task UpdateStatus_NonExistent_Returns404()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "order-admin-status-404@test.com");

        HttpResponseMessage response = await client.PutAsJsonAsync($"/api/orders/admin/{Guid.Empty}/status", new
        {
            status = "Shipped"
        });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteOrder_AsAdmin_Returns204()
    {
        HttpClient client = _factory.CreateClient();
        var (_, auth) = await RegisterAdmin(client, _factory, "order-admin-delete@test.com");
        Guid orderId = await SeedOrder(auth.User.Email);

        HttpResponseMessage response = await client.DeleteAsync($"/api/orders/admin/{orderId}");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        HttpResponseMessage getResponse = await client.GetAsync($"/api/orders/{orderId}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task DeleteOrder_NonExistent_Returns404()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "order-admin-delete-404@test.com");

        HttpResponseMessage response = await client.DeleteAsync($"/api/orders/admin/{Guid.Empty}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
