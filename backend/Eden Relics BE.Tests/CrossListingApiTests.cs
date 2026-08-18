using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Eden_Relics_BE.Data;
using Eden_Relics_BE.Data.Entities;
using Microsoft.Extensions.DependencyInjection;
using static Eden_Relics_BE.Tests.Helpers;

namespace Eden_Relics_BE.Tests;

/// <summary>
/// Cross-listing through the API: who may see it, what the preview says per platform, and the relist
/// floor that exists to keep a seller's Vinted account safe rather than to be convenient.
/// </summary>
public class CrossListingApiTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public CrossListingApiTests(ApiFactory factory)
    {
        _factory = factory;
    }

    private static async Task<Guid> CreateProduct(HttpClient client, string name, string era = "1970s")
    {
        HttpResponseMessage res = await client.PostAsJsonAsync("/api/products", new
        {
            name,
            description = "A navy floral prairie maxi.",
            price = 60m,
            era,
            category = "70s",
            size = "10",
            condition = "very good",
            imageUrl = "https://img.test/a.webp",
            inStock = true,
        });
        res.EnsureSuccessStatusCode();
        ProductResponse? created = await res.Content.ReadFromJsonAsync<ProductResponse>(JsonOptions);
        return created!.Id;
    }

    /// <summary>Adds a listing and backdates it, which is the only way to exercise relist ages.</summary>
    private async Task AddAgedListing(Guid productId, string platform, int daysOld)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        EdenRelicsDbContext db = scope.ServiceProvider.GetRequiredService<EdenRelicsDbContext>();
        ProductListing listing = new()
        {
            ProductId = productId,
            Platform = platform,
            Status = "Active",
        };
        db.Set<ProductListing>().Add(listing);
        await db.SaveChangesAsync();
        listing.CreatedAtUtc = DateTime.UtcNow.AddDays(-daysOld);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Preview_IsAdminOnly()
    {
        HttpClient user = _factory.CreateClient();
        await RegisterAndLogin(user, "user-crosslist@test.com");
        Guid id = Guid.NewGuid();
        Assert.Equal(HttpStatusCode.Forbidden, (await user.GetAsync($"/api/cross-listing/preview/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await user.GetAsync("/api/cross-listing/relist-candidates")).StatusCode);
    }

    [Fact]
    public async Task Preview_ReportsEveryPlatform_EtsyReadyAndTheRestHonestAboutWhy()
    {
        HttpClient admin = _factory.CreateClient();
        await RegisterAdmin(admin, _factory, "admin-crosslist-preview@test.com");
        Guid id = await CreateProduct(admin, "Folk Floral Maxi Dress");

        HttpResponseMessage res = await admin.GetAsync($"/api/cross-listing/preview/{id}");
        res.EnsureSuccessStatusCode();
        JsonElement r = JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement;

        JsonElement platforms = r.GetProperty("platforms");
        Assert.Equal(4, platforms.GetArrayLength());

        Dictionary<string, JsonElement> byName = platforms.EnumerateArray()
            .ToDictionary(p => p.GetProperty("platform").GetString()!);
        Assert.True(byName.ContainsKey("Etsy") && byName.ContainsKey("eBay")
            && byName.ContainsKey("Vinted") && byName.ContainsKey("Depop"));

        // Etsy is the one platform whose fields we actually know.
        Assert.True(byName["Etsy"].GetProperty("validation").GetProperty("canPublish").GetBoolean());
        // The API serialises enums kebab-cased, so this is "server-api" over the wire.
        Assert.Equal("server-api", byName["Etsy"].GetProperty("transport").GetString());

        // The others refuse, and say what would unblock them rather than failing silently.
        foreach (string platform in new[] { "eBay", "Vinted", "Depop" })
        {
            JsonElement p = byName[platform];
            Assert.False(p.GetProperty("validation").GetProperty("canPublish").GetBoolean());
            Assert.NotEmpty(p.GetProperty("validation").GetProperty("blocking").EnumerateArray());
        }

        // Extension platforms hand over pasteable content regardless.
        foreach (string platform in new[] { "Vinted", "Depop" })
        {
            JsonElement fallback = byName[platform].GetProperty("fallback");
            Assert.NotEqual(JsonValueKind.Null, fallback.ValueKind);
            Assert.False(string.IsNullOrWhiteSpace(fallback.GetProperty("title").GetString()));
        }
    }

    [Fact]
    public async Task Preview_WithAnUnresolvableEra_BlocksEtsyToo()
    {
        HttpClient admin = _factory.CreateClient();
        await RegisterAdmin(admin, _factory, "admin-crosslist-era@test.com");
        Guid id = await CreateProduct(admin, "Mystery Dress", era: "vintage");

        JsonElement r = JsonDocument.Parse(
            await (await admin.GetAsync($"/api/cross-listing/preview/{id}")).Content.ReadAsStringAsync()).RootElement;

        JsonElement etsy = r.GetProperty("platforms").EnumerateArray()
            .First(p => p.GetProperty("platform").GetString() == "Etsy");
        Assert.False(etsy.GetProperty("validation").GetProperty("canPublish").GetBoolean());
        Assert.Contains(
            etsy.GetProperty("validation").GetProperty("blocking").EnumerateArray(),
            b => b.GetProperty("field").GetString() == "Era");
    }

    [Fact]
    public async Task Preview_ForAnUnknownProduct_Is404()
    {
        HttpClient admin = _factory.CreateClient();
        await RegisterAdmin(admin, _factory, "admin-crosslist-404@test.com");
        Assert.Equal(HttpStatusCode.NotFound,
            (await admin.GetAsync($"/api/cross-listing/preview/{Guid.NewGuid()}")).StatusCode);
    }

    [Fact]
    public async Task RelistCandidates_OffersOldStock_AndNeverAnythingUnderTheFloor()
    {
        HttpClient admin = _factory.CreateClient();
        await RegisterAdmin(admin, _factory, "admin-crosslist-relist@test.com");

        Guid stale = await CreateProduct(admin, "Listed Ages Ago");
        Guid recent = await CreateProduct(admin, "Listed Last Week");
        Guid middling = await CreateProduct(admin, "Listed Ten Weeks Ago");

        await AddAgedListing(stale, "Vinted", daysOld: 120);
        await AddAgedListing(recent, "Vinted", daysOld: 7);
        // Past the 60-day floor but short of the 90-day offer cadence: still not offered.
        await AddAgedListing(middling, "Vinted", daysOld: 70);

        JsonElement candidates = JsonDocument.Parse(
            await (await admin.GetAsync("/api/cross-listing/relist-candidates")).Content.ReadAsStringAsync()).RootElement;

        List<Guid> ids = [.. candidates.EnumerateArray().Select(c => c.GetProperty("productId").GetGuid())];
        Assert.Contains(stale, ids);
        Assert.DoesNotContain(recent, ids);
        Assert.DoesNotContain(middling, ids);

        // Nothing offered is ever under the floor — the rule that protects the account.
        Assert.All(candidates.EnumerateArray(), c =>
            Assert.True(c.GetProperty("daysListed").GetInt32() >= 60));
    }

    [Fact]
    public async Task RelistCandidates_SkipsPiecesThatHaveSold()
    {
        HttpClient admin = _factory.CreateClient();
        await RegisterAdmin(admin, _factory, "admin-crosslist-sold@test.com");
        Guid sold = await CreateProduct(admin, "Long Gone Dress");
        await AddAgedListing(sold, "Vinted", daysOld: 200);

        (await admin.PutAsJsonAsync($"/api/products/{sold}", new { status = "sold" })).EnsureSuccessStatusCode();

        JsonElement candidates = JsonDocument.Parse(
            await (await admin.GetAsync("/api/cross-listing/relist-candidates")).Content.ReadAsStringAsync()).RootElement;

        Assert.DoesNotContain(sold, candidates.EnumerateArray().Select(c => c.GetProperty("productId").GetGuid()));
    }
}
