using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using static Eden_Relics_BE.Tests.Helpers;

namespace Eden_Relics_BE.Tests;

public class AccountsSummaryTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public AccountsSummaryTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetSummary_AsAdmin_ReturnsMetrics()
    {
        var client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "accounts-summary@test.com");

        var response = await client.GetAsync("/api/accounts/summary");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        var summary = JsonDocument.Parse(json).RootElement;

        Assert.True(summary.TryGetProperty("totalRevenue", out _));
        Assert.True(summary.TryGetProperty("totalCost", out _));
        Assert.True(summary.TryGetProperty("totalProfit", out _));
        Assert.True(summary.TryGetProperty("marginPercent", out _));
        Assert.True(summary.TryGetProperty("totalOrders", out _));
        Assert.True(summary.TryGetProperty("totalItemsSold", out _));
        Assert.True(summary.TryGetProperty("averageOrderValue", out _));
        Assert.True(summary.TryGetProperty("revenueByMonth", out _));
        Assert.True(summary.TryGetProperty("revenueByCategory", out _));
        Assert.True(summary.TryGetProperty("revenueByEra", out _));
        Assert.True(summary.TryGetProperty("inventory", out _));
        Assert.True(summary.TryGetProperty("ordersByStatus", out _));

        var inventory = summary.GetProperty("inventory");
        Assert.True(inventory.TryGetProperty("inStock", out _));
        Assert.True(inventory.TryGetProperty("outOfStock", out _));
        Assert.True(inventory.TryGetProperty("retailValue", out _));
        Assert.True(inventory.TryGetProperty("costValue", out _));
    }

    [Fact]
    public async Task GetSummary_Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/accounts/summary");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetSummary_AsCustomer_Returns403()
    {
        var client = _factory.CreateClient();
        await RegisterAndLogin(client, "accounts-customer@test.com");
        var response = await client.GetAsync("/api/accounts/summary");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
