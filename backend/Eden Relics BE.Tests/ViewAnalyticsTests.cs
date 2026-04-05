using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using static Eden_Relics_BE.Tests.Helpers;

namespace Eden_Relics_BE.Tests;

public class ViewAnalyticsTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public ViewAnalyticsTests(ApiFactory factory)
    {
        _factory = factory;
    }

    private async Task<ProductResponse> CreateProduct(HttpClient client, string name)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/products", new
        {
            name,
            description = "Desc",
            price = 100m,
            era = "1990s",
            category = "90s",
            size = "M",
            condition = "good",
            imageUrl = "https://example.com/img.webp",
            inStock = true
        });
        return (await response.Content.ReadFromJsonAsync<ProductResponse>(JsonOptions))!;
    }

    [Fact]
    public async Task GetViewAnalytics_AsAdmin_ReturnsAnalytics()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "analytics-get@test.com");
        ProductResponse product = await CreateProduct(client, "Analytics Dress");

        // Record a view
        await client.PostAsync($"/api/products/{product.Id}/view", null);

        HttpResponseMessage response = await client.GetAsync($"/api/products/{product.Id}/views");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        string json = await response.Content.ReadAsStringAsync();
        JsonElement analytics = JsonDocument.Parse(json).RootElement;
        Assert.True(analytics.GetProperty("totalViews").GetInt32() >= 1);
        Assert.True(analytics.TryGetProperty("trackedViews", out _));
        Assert.True(analytics.TryGetProperty("views", out _));
        Assert.True(analytics.TryGetProperty("byChannel", out _));
        Assert.True(analytics.TryGetProperty("byCountry", out _));
        Assert.True(analytics.TryGetProperty("topReferrers", out _));
        Assert.True(analytics.TryGetProperty("viewsByDate", out _));
        Assert.True(analytics.TryGetProperty("byDevice", out _));
        Assert.True(analytics.TryGetProperty("byOs", out _));
    }

    [Fact]
    public async Task GetViewAnalytics_NonExistentProduct_Returns404()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "analytics-404@test.com");

        HttpResponseMessage response = await client.GetAsync($"/api/products/{Guid.Empty}/views");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetViewAnalytics_Unauthenticated_Returns401()
    {
        HttpClient client = _factory.CreateClient();
        Guid id = Guid.Parse("a1b2c3d4-0001-0000-0000-000000000001");
        HttpResponseMessage response = await client.GetAsync($"/api/products/{id}/views");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetViewAnalytics_AsCustomer_Returns403()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAndLogin(client, "analytics-customer@test.com");
        Guid id = Guid.Parse("a1b2c3d4-0001-0000-0000-000000000001");
        HttpResponseMessage response = await client.GetAsync($"/api/products/{id}/views");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task RecordView_WithReferrer_TracksChannel()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "analytics-referrer@test.com");
        ProductResponse product = await CreateProduct(client, "Referrer Dress");

        HttpResponseMessage response = await client.PostAsJsonAsync($"/api/products/{product.Id}/view", new
        {
            referrer = "https://www.google.com/search?q=vintage+dress",
            utmSource = "google",
            utmMedium = "cpc",
            utmCampaign = "spring-sale"
        });
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        HttpResponseMessage analytics = await client.GetAsync($"/api/products/{product.Id}/views");
        string json = await analytics.Content.ReadAsStringAsync();
        JsonElement data = JsonDocument.Parse(json).RootElement;
        Assert.True(data.GetProperty("trackedViews").GetInt32() >= 1);
    }
}
