using System.Net;
using System.Net.Http.Json;
using static Eden_Relics_BE.Tests.Helpers;

namespace Eden_Relics_BE.Tests;

public class MarketplaceTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public MarketplaceTests(ApiFactory factory)
    {
        _factory = factory;
    }

    private async Task<ProductResponse> CreateTestProduct(HttpClient client, string name = "Marketplace Dress")
    {
        var response = await client.PostAsJsonAsync("/api/products", new
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
    public async Task GetListings_EmptyProduct_ReturnsEmptyList()
    {
        var client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "market-getlist@test.com");
        var product = await CreateTestProduct(client, "No Listings Dress");

        var listings = await client.GetFromJsonAsync<List<ListingResponse>>($"/api/marketplace/listings/{product.Id}", JsonOptions);
        Assert.NotNull(listings);
        Assert.Empty(listings);
    }

    [Fact]
    public async Task AddListing_AsAdmin_ReturnsListing()
    {
        var client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "market-add@test.com");
        var product = await CreateTestProduct(client, "Listed Dress");

        var response = await client.PostAsJsonAsync("/api/marketplace/listings", new
        {
            productId = product.Id,
            platform = "Depop",
            externalListingId = "depop-123",
            externalUrl = "https://depop.com/listing/123"
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var listing = await response.Content.ReadFromJsonAsync<ListingResponse>(JsonOptions);
        Assert.NotNull(listing);
        Assert.Equal("Depop", listing.Platform);
        Assert.Equal("Active", listing.Status);
        Assert.Equal("depop-123", listing.ExternalListingId);
    }

    [Fact]
    public async Task AddListing_NonExistentProduct_Returns404()
    {
        var client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "market-add-404@test.com");

        var response = await client.PostAsJsonAsync("/api/marketplace/listings", new
        {
            productId = Guid.Empty,
            platform = "Depop"
        });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateListingStatus_Sold_MarksProductOutOfStock()
    {
        var client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "market-sold@test.com");
        var product = await CreateTestProduct(client, "Sold Dress");

        // Add two listings
        var resp1 = await client.PostAsJsonAsync("/api/marketplace/listings", new
        {
            productId = product.Id,
            platform = "Depop"
        });
        var listing1 = await resp1.Content.ReadFromJsonAsync<ListingResponse>(JsonOptions);

        await client.PostAsJsonAsync("/api/marketplace/listings", new
        {
            productId = product.Id,
            platform = "Vinted"
        });

        // Mark first as sold
        var updateResponse = await client.PutAsJsonAsync($"/api/marketplace/listings/{listing1!.Id}/status", new
        {
            status = "Sold"
        });
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var updated = await updateResponse.Content.ReadFromJsonAsync<ListingResponse>(JsonOptions);
        Assert.Equal("Sold", updated!.Status);

        // Product should be out of stock
        var productResponse = await client.GetFromJsonAsync<ProductResponse>($"/api/products/{product.Id}", JsonOptions);
        Assert.False(productResponse!.InStock);
    }

    [Fact]
    public async Task UpdateListingStatus_NonExistent_Returns404()
    {
        var client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "market-update-404@test.com");

        var response = await client.PutAsJsonAsync($"/api/marketplace/listings/{Guid.Empty}/status", new
        {
            status = "Sold"
        });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task RemoveListing_AsAdmin_Returns204()
    {
        var client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "market-remove@test.com");
        var product = await CreateTestProduct(client, "Remove Listing Dress");

        var addResp = await client.PostAsJsonAsync("/api/marketplace/listings", new
        {
            productId = product.Id,
            platform = "eBay"
        });
        var listing = await addResp.Content.ReadFromJsonAsync<ListingResponse>(JsonOptions);

        var response = await client.DeleteAsync($"/api/marketplace/listings/{listing!.Id}");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task RemoveListing_NonExistent_Returns404()
    {
        var client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "market-remove-404@test.com");
        var response = await client.DeleteAsync($"/api/marketplace/listings/{Guid.Empty}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task MarkSold_AsAdmin_FlagsOtherListings()
    {
        var client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "market-marksold@test.com");
        var product = await CreateTestProduct(client, "Mark Sold Dress");

        await client.PostAsJsonAsync("/api/marketplace/listings", new
        {
            productId = product.Id,
            platform = "Website"
        });
        await client.PostAsJsonAsync("/api/marketplace/listings", new
        {
            productId = product.Id,
            platform = "Depop"
        });

        var response = await client.PostAsJsonAsync($"/api/marketplace/mark-sold/{product.Id}", new
        {
            soldOn = "Website"
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Check pending removals
        var pending = await client.GetFromJsonAsync<List<PendingRemovalResponse>>("/api/marketplace/pending-removals", JsonOptions);
        Assert.NotNull(pending);
        Assert.Contains(pending, p => p.ProductId == product.Id && p.Platform == "Depop");
    }

    [Fact]
    public async Task MarkSold_NonExistentProduct_Returns404()
    {
        var client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "market-marksold-404@test.com");

        var response = await client.PostAsJsonAsync($"/api/marketplace/mark-sold/{Guid.Empty}", new
        {
            soldOn = "Website"
        });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AcknowledgeRemoval_AsAdmin_Returns200()
    {
        var client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "market-ack@test.com");
        var product = await CreateTestProduct(client, "Ack Removal Dress");

        var addResp = await client.PostAsJsonAsync("/api/marketplace/listings", new
        {
            productId = product.Id,
            platform = "Depop"
        });
        var listing = await addResp.Content.ReadFromJsonAsync<ListingResponse>(JsonOptions);

        // Set to PendingRemoval via mark-sold
        await client.PostAsJsonAsync($"/api/marketplace/mark-sold/{product.Id}", new
        {
            soldOn = "Website"
        });

        var response = await client.PostAsync($"/api/marketplace/acknowledge-removal/{listing!.Id}", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AcknowledgeRemoval_NonExistent_Returns404()
    {
        var client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "market-ack-404@test.com");

        var response = await client.PostAsync($"/api/marketplace/acknowledge-removal/{Guid.Empty}", null);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task EtsyStatus_ReturnsNotConfigured()
    {
        var client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "market-etsy-status@test.com");

        var response = await client.GetAsync("/api/marketplace/etsy/status");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<EtsyStatusResponse>(JsonOptions);
        Assert.NotNull(result);
        Assert.False(result.Configured);
    }

    [Fact]
    public async Task CreateEtsyListing_NotConfigured_Returns400()
    {
        var client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "market-etsy-create@test.com");
        var product = await CreateTestProduct(client, "Etsy Dress");

        var response = await client.PostAsJsonAsync("/api/marketplace/etsy/create-listing", new
        {
            productId = product.Id
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GenerateListingText_Depop_ReturnsFormattedText()
    {
        var client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "market-gen-depop@test.com");
        var product = await CreateTestProduct(client, "Depop Text Dress");

        var result = await client.GetFromJsonAsync<GeneratedListingResponse>(
            $"/api/marketplace/generate-listing/{product.Id}?platform=Depop", JsonOptions);
        Assert.NotNull(result);
        Assert.Contains("Depop Text Dress", result.Title);
        Assert.Contains("1990s", result.Title);
        Assert.Contains("#vintage", result.Description);
    }

    [Fact]
    public async Task GenerateListingText_Vinted_ReturnsFormattedText()
    {
        var client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "market-gen-vinted@test.com");
        var product = await CreateTestProduct(client, "Vinted Text Dress");

        var result = await client.GetFromJsonAsync<GeneratedListingResponse>(
            $"/api/marketplace/generate-listing/{product.Id}?platform=Vinted", JsonOptions);
        Assert.NotNull(result);
        Assert.Contains("Vinted Text Dress", result.Title);
        Assert.Contains("edenrelics.co.uk", result.Description);
    }

    [Fact]
    public async Task GenerateListingText_NonExistent_Returns404()
    {
        var client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "market-gen-404@test.com");

        var response = await client.GetAsync($"/api/marketplace/generate-listing/{Guid.Empty}?platform=Depop");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AllEndpoints_Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient();
        var id = Guid.NewGuid();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync($"/api/marketplace/listings/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/marketplace/listings", new { productId = id, platform = "X" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/marketplace/pending-removals")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/marketplace/etsy/status")).StatusCode);
    }

    [Fact]
    public async Task AllEndpoints_AsCustomer_Returns403()
    {
        var client = _factory.CreateClient();
        await RegisterAndLogin(client, "market-customer@test.com");
        var id = Guid.NewGuid();

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/marketplace/listings/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/marketplace/listings", new { productId = id, platform = "X" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/marketplace/pending-removals")).StatusCode);
    }

    private record ListingResponse(Guid Id, Guid ProductId, string Platform, string? ExternalListingId, string? ExternalUrl, string Status);
    private record PendingRemovalResponse(Guid ListingId, Guid ProductId, string ProductName, string Platform, string? ExternalUrl);
    private record EtsyStatusResponse(bool Configured);
    private record GeneratedListingResponse(string Title, string Description, decimal Price, string ImageUrl);
}
