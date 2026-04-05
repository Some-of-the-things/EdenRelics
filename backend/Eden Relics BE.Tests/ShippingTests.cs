using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using static Eden_Relics_BE.Tests.Helpers;

namespace Eden_Relics_BE.Tests;

public class ShippingTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public ShippingTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetCountries_ReturnsAllZones()
    {
        HttpClient client = _factory.CreateClient();
        HttpResponseMessage response = await client.GetAsync("/api/shipping/countries");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        string json = await response.Content.ReadAsStringAsync();
        JsonElement zones = JsonDocument.Parse(json).RootElement;
        Assert.True(zones.GetArrayLength() >= 7, "Expected at least 7 shipping zones");

        // Verify structure of first zone
        JsonElement first = zones[0];
        Assert.True(first.TryGetProperty("zone", out _));
        Assert.True(first.TryGetProperty("label", out _));
        Assert.True(first.TryGetProperty("deliveryEstimate", out _));
        Assert.True(first.TryGetProperty("price", out _));
        Assert.True(first.TryGetProperty("countries", out _));
    }

    [Fact]
    public async Task GetCountries_ContainsUK()
    {
        HttpClient client = _factory.CreateClient();
        string json = await client.GetStringAsync("/api/shipping/countries");
        Assert.Contains("United Kingdom", json);
        Assert.Contains("GB", json);
    }

    [Fact]
    public async Task GetCountries_ContainsInternationalCountries()
    {
        HttpClient client = _factory.CreateClient();
        string json = await client.GetStringAsync("/api/shipping/countries");
        Assert.Contains("France", json);
        Assert.Contains("United States", json);
        Assert.Contains("Australia", json);
        Assert.Contains("Japan", json);
    }

    [Fact]
    public async Task GetRate_UK_ReturnsStandardRate()
    {
        HttpClient client = _factory.CreateClient();
        ShippingRateResponse? rate = await client.GetFromJsonAsync<ShippingRateResponse>("/api/shipping/rate?country=GB", JsonOptions);
        Assert.NotNull(rate);
        Assert.Equal(3.95m, rate.Price);
        Assert.Equal("standard", rate.Method);
    }

    [Fact]
    public async Task GetRate_ByCountryName_Works()
    {
        HttpClient client = _factory.CreateClient();
        ShippingRateResponse? rate = await client.GetFromJsonAsync<ShippingRateResponse>("/api/shipping/rate?country=France", JsonOptions);
        Assert.NotNull(rate);
        Assert.Equal(9.95m, rate.Price);
        Assert.Equal("europe", rate.Method);
    }

    [Fact]
    public async Task GetRate_ByCountryCode_Works()
    {
        HttpClient client = _factory.CreateClient();
        ShippingRateResponse? rate = await client.GetFromJsonAsync<ShippingRateResponse>("/api/shipping/rate?country=US", JsonOptions);
        Assert.NotNull(rate);
        Assert.Equal(14.95m, rate.Price);
        Assert.Equal("international", rate.Method);
    }

    [Fact]
    public async Task GetRate_Australia_ReturnsAustralasiaRate()
    {
        HttpClient client = _factory.CreateClient();
        ShippingRateResponse? rate = await client.GetFromJsonAsync<ShippingRateResponse>("/api/shipping/rate?country=AU", JsonOptions);
        Assert.NotNull(rate);
        Assert.Equal(16.95m, rate.Price);
    }

    [Fact]
    public async Task GetRate_Japan_ReturnsAsiaRate()
    {
        HttpClient client = _factory.CreateClient();
        ShippingRateResponse? rate = await client.GetFromJsonAsync<ShippingRateResponse>("/api/shipping/rate?country=JP", JsonOptions);
        Assert.NotNull(rate);
        Assert.Equal(14.95m, rate.Price);
    }

    [Fact]
    public async Task GetRate_SouthAfrica_ReturnsRestOfWorldRate()
    {
        HttpClient client = _factory.CreateClient();
        ShippingRateResponse? rate = await client.GetFromJsonAsync<ShippingRateResponse>("/api/shipping/rate?country=ZA", JsonOptions);
        Assert.NotNull(rate);
        Assert.Equal(18.95m, rate.Price);
    }

    [Fact]
    public async Task GetRate_UnsupportedCountry_Returns400()
    {
        HttpClient client = _factory.CreateClient();
        HttpResponseMessage response = await client.GetAsync("/api/shipping/rate?country=XX");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetRate_EmptyCountry_Returns400()
    {
        HttpClient client = _factory.CreateClient();
        HttpResponseMessage response = await client.GetAsync("/api/shipping/rate?country=");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetRate_MissingCountry_Returns400()
    {
        HttpClient client = _factory.CreateClient();
        HttpResponseMessage response = await client.GetAsync("/api/shipping/rate");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetRate_CaseInsensitive()
    {
        HttpClient client = _factory.CreateClient();
        ShippingRateResponse? rate = await client.GetFromJsonAsync<ShippingRateResponse>("/api/shipping/rate?country=gb", JsonOptions);
        Assert.NotNull(rate);
        Assert.Equal(3.95m, rate.Price);
    }

    private record ShippingRateResponse(string Zone, string Label, string DeliveryEstimate, decimal Price, string Method);
}
