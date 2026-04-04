using System.Net;
using System.Net.Http.Json;
using static Eden_Relics_BE.Tests.Helpers;

namespace Eden_Relics_BE.Tests;

/// <summary>
/// Tests for zone-based shipping cost calculation in the orders flow.
/// Note: Order creation calls Stripe which is not configured in tests,
/// so we test the shipping rate lookup logic via the shipping API
/// and verify the order controller validates correctly.
/// </summary>
public class OrderShippingTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    private static readonly Guid SeededProductId = Guid.Parse("a1b2c3d4-0001-0000-0000-000000000001");

    public OrderShippingTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ShippingRate_Europe_MatchesZoneConfig()
    {
        var client = _factory.CreateClient();

        // Verify Europe zone pricing via shipping API
        var rate = await client.GetFromJsonAsync<RateResponse>("/api/shipping/rate?country=DE", JsonOptions);
        Assert.NotNull(rate);
        Assert.Equal(9.95m, rate.Price);
        Assert.Equal("europe", rate.Zone);
    }

    [Fact]
    public async Task ShippingRate_AllEuropeCountries_SamePrice()
    {
        var client = _factory.CreateClient();
        string[] euCountries = ["FR", "DE", "NL", "BE", "ES", "PT", "IT", "AT", "SE", "DK", "NO", "FI", "PL", "CZ", "GR", "HR", "HU", "RO", "LU", "IE", "CH"];

        foreach (var code in euCountries)
        {
            var rate = await client.GetFromJsonAsync<RateResponse>($"/api/shipping/rate?country={code}", JsonOptions);
            Assert.NotNull(rate);
            if (code == "CH")
            {
                // Switzerland is in Europe zone
                Assert.Equal(9.95m, rate.Price);
            }
            else
            {
                Assert.Equal(9.95m, rate.Price);
            }
        }
    }

    [Fact]
    public async Task ShippingRate_MiddleEast_CorrectPrice()
    {
        var client = _factory.CreateClient();
        string[] meCountries = ["AE", "SA", "QA", "IL"];

        foreach (var code in meCountries)
        {
            var rate = await client.GetFromJsonAsync<RateResponse>($"/api/shipping/rate?country={code}", JsonOptions);
            Assert.NotNull(rate);
            Assert.Equal(14.95m, rate.Price);
        }
    }

    [Fact]
    public async Task Create_WithNoEmail_AndNoAuth_Returns400()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/orders", new
        {
            items = new[] { new { productId = SeededProductId, quantity = 1 } },
            shippingAddress = new { addressLine1 = "1 Rue de Rivoli", city = "Paris", postcode = "75001", country = "FR" },
            shippingMethod = "europe"
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithInvalidProduct_Returns400()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/orders", new
        {
            items = new[] { new { productId = Guid.Empty, quantity = 1 } },
            guestEmail = "intl@test.com",
            shippingAddress = new { addressLine1 = "123 Main St", city = "New York", postcode = "10001", country = "US" },
            shippingMethod = "international"
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private record RateResponse(string Zone, string Label, string DeliveryEstimate, decimal Price, string Method);
}
