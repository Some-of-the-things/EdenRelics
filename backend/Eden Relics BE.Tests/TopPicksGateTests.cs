using System.Net;
using System.Net.Http.Json;
using static Eden_Relics_BE.Tests.Helpers;

namespace Eden_Relics_BE.Tests;

/// <summary>
/// The admin on/off switch for the public "Our Top Picks" surfaces. The gate has to be flippable
/// from the browser (no redeploy), has to persist, and must never take the curated list with it.
///
/// Each test builds its own <see cref="ApiFactory"/> rather than sharing a class fixture: the gate
/// is a single global row, so tests sharing one in-memory database would flip it under each other.
/// </summary>
public class TopPicksGateTests
{
    private record TopPickItem(Guid ProductId, bool Featured);
    private record AdminPayload(bool Enabled, List<TopPickItem> Items);
    private record PublicPayload(bool Enabled, List<Guid> ProductIds, List<Guid> FeaturedProductIds);

    private static async Task<Guid> CreateProduct(HttpClient client, string name)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/products", new
        {
            name,
            description = "Desc",
            price = 100m,
            era = "1970s",
            category = "70s",
            size = "M",
            condition = "good",
            imageUrl = "https://example.com/img.webp",
            inStock = true
        });
        response.EnsureSuccessStatusCode();
        ProductResponse? product = await response.Content.ReadFromJsonAsync<ProductResponse>(JsonOptions);
        return product!.Id;
    }

    [Fact]
    public async Task PublicGate_WithNoStoredChoice_FallsBackToConfiguredDefault()
    {
        using ApiFactory factory = new();
        HttpClient client = factory.CreateClient();

        PublicPayload? payload = await client.GetFromJsonAsync<PublicPayload>("/api/top-picks", JsonOptions);

        // Test configuration leaves TopPicks:Enabled at its default of false, and no admin has
        // stored a choice, so the surfaces stay dormant.
        Assert.NotNull(payload);
        Assert.False(payload.Enabled);
        Assert.Empty(payload.ProductIds);
    }

    [Fact]
    public async Task SetEnabled_AsAdmin_TurnsThePublicSurfacesOn()
    {
        using ApiFactory factory = new();
        HttpClient client = factory.CreateClient();
        await RegisterAdmin(client, factory, "toppicks-on@test.com");
        Guid productId = await CreateProduct(client, "Gate On Dress");

        HttpResponseMessage saved = await client.PutAsJsonAsync("/api/top-picks/admin", new
        {
            items = new[] { new { productId, featured = true } }
        });
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);

        HttpResponseMessage toggled = await client.PutAsJsonAsync("/api/top-picks/admin/enabled", new { enabled = true });
        Assert.Equal(HttpStatusCode.OK, toggled.StatusCode);
        AdminPayload? admin = await toggled.Content.ReadFromJsonAsync<AdminPayload>(JsonOptions);
        Assert.True(admin!.Enabled);

        PublicPayload? payload = await client.GetFromJsonAsync<PublicPayload>("/api/top-picks", JsonOptions);
        Assert.True(payload!.Enabled);
        Assert.Contains(productId, payload.ProductIds);
        Assert.Contains(productId, payload.FeaturedProductIds);
    }

    [Fact]
    public async Task SetEnabledFalse_HidesThePublicSurfacesButKeepsTheCuratedList()
    {
        using ApiFactory factory = new();
        HttpClient client = factory.CreateClient();
        await RegisterAdmin(client, factory, "toppicks-off@test.com");
        Guid productId = await CreateProduct(client, "Gate Off Dress");

        await client.PutAsJsonAsync("/api/top-picks/admin", new
        {
            items = new[] { new { productId, featured = true } }
        });
        await client.PutAsJsonAsync("/api/top-picks/admin/enabled", new { enabled = true });

        HttpResponseMessage off = await client.PutAsJsonAsync("/api/top-picks/admin/enabled", new { enabled = false });
        Assert.Equal(HttpStatusCode.OK, off.StatusCode);

        // Public surfaces go dark...
        PublicPayload? payload = await client.GetFromJsonAsync<PublicPayload>("/api/top-picks", JsonOptions);
        Assert.False(payload!.Enabled);
        Assert.Empty(payload.ProductIds);

        // ...but the curation survives, so switching back on restores the same edit.
        AdminPayload? admin = await client.GetFromJsonAsync<AdminPayload>("/api/top-picks/admin", JsonOptions);
        Assert.False(admin!.Enabled);
        Assert.Contains(admin.Items, i => i.ProductId == productId);
    }

    [Fact]
    public async Task StoredChoice_IsReadBackOnAFreshRequest()
    {
        using ApiFactory factory = new();
        HttpClient client = factory.CreateClient();
        await RegisterAdmin(client, factory, "toppicks-persist@test.com");

        await client.PutAsJsonAsync("/api/top-picks/admin/enabled", new { enabled = true });

        // A separate, anonymous client goes through a fresh request scope and DbContext, so this
        // only passes if the choice was actually stored rather than held in the writing scope.
        HttpClient anonymous = factory.CreateClient();
        PublicPayload? payload = await anonymous.GetFromJsonAsync<PublicPayload>("/api/top-picks", JsonOptions);
        Assert.True(payload!.Enabled);
    }

    [Fact]
    public async Task RepeatedToggling_KeepsASingleStoredChoice()
    {
        using ApiFactory factory = new();
        HttpClient client = factory.CreateClient();
        await RegisterAdmin(client, factory, "toppicks-repeat@test.com");

        // Updating in place rather than appending a row each time is what keeps the "oldest row
        // wins" read honest — otherwise the first save would shadow every later one.
        await client.PutAsJsonAsync("/api/top-picks/admin/enabled", new { enabled = true });
        await client.PutAsJsonAsync("/api/top-picks/admin/enabled", new { enabled = false });
        await client.PutAsJsonAsync("/api/top-picks/admin/enabled", new { enabled = true });

        HttpClient anonymous = factory.CreateClient();
        PublicPayload? payload = await anonymous.GetFromJsonAsync<PublicPayload>("/api/top-picks", JsonOptions);
        Assert.True(payload!.Enabled);
    }

    [Fact]
    public async Task SetEnabled_Anonymously_IsRejected()
    {
        using ApiFactory factory = new();
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PutAsJsonAsync("/api/top-picks/admin/enabled", new { enabled = true });

        Assert.True(
            response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden,
            $"Expected 401/403 for an anonymous gate flip, got {(int)response.StatusCode}.");
    }
}
