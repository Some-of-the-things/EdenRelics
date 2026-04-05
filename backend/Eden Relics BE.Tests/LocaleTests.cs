using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using static Eden_Relics_BE.Tests.Helpers;

namespace Eden_Relics_BE.Tests;

public class LocaleTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public LocaleTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Detect_ReturnsDefaultUK_WhenNoGeoIp()
    {
        HttpClient client = _factory.CreateClient();
        HttpResponseMessage response = await client.GetAsync("/api/locale/detect");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        string json = await response.Content.ReadAsStringAsync();
        JsonElement result = JsonDocument.Parse(json).RootElement;

        Assert.Equal("GB", result.GetProperty("countryCode").GetString());
        Assert.Equal("GBP", result.GetProperty("currency").GetString());
        Assert.Equal("en-GB", result.GetProperty("locale").GetString());
        Assert.Equal(1m, result.GetProperty("exchangeRate").GetDecimal());
    }

    [Fact]
    public async Task Detect_ReturnsCurrencySymbol()
    {
        HttpClient client = _factory.CreateClient();
        LocaleResponse? result = await client.GetFromJsonAsync<LocaleResponse>("/api/locale/detect", JsonOptions);
        Assert.NotNull(result);
        Assert.False(string.IsNullOrEmpty(result.CurrencySymbol));
    }

    [Fact]
    public async Task GetRates_ReturnsAllCurrencies()
    {
        HttpClient client = _factory.CreateClient();
        HttpResponseMessage response = await client.GetAsync("/api/locale/rates");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        Dictionary<string, decimal>? rates = await response.Content.ReadFromJsonAsync<Dictionary<string, decimal>>(JsonOptions);
        Assert.NotNull(rates);
        Assert.True(rates.ContainsKey("GBP"));
        Assert.Equal(1m, rates["GBP"]);
        Assert.True(rates.ContainsKey("EUR"));
        Assert.True(rates.ContainsKey("USD"));
        Assert.True(rates.ContainsKey("JPY"));
        Assert.True(rates["EUR"] > 1m);
        Assert.True(rates["USD"] > 1m);
        Assert.True(rates["JPY"] > 100m);
    }

    [Fact]
    public async Task GetRates_ContainsAllShippingCountryCurrencies()
    {
        HttpClient client = _factory.CreateClient();
        Dictionary<string, decimal>? rates = await client.GetFromJsonAsync<Dictionary<string, decimal>>("/api/locale/rates", JsonOptions);
        Assert.NotNull(rates);

        // Verify currencies for all shipping zones are present
        Assert.True(rates.ContainsKey("AUD")); // Australia
        Assert.True(rates.ContainsKey("CAD")); // Canada
        Assert.True(rates.ContainsKey("CHF")); // Switzerland
        Assert.True(rates.ContainsKey("SEK")); // Sweden
        Assert.True(rates.ContainsKey("AED")); // UAE
        Assert.True(rates.ContainsKey("ZAR")); // South Africa
    }

    private record LocaleResponse(bool Detected, string CountryCode, string CountryName, string Currency, string CurrencySymbol, string Locale, decimal ExchangeRate);
}
