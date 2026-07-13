using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using static Eden_Relics_BE.Tests.Helpers;

namespace Eden_Relics_BE.Tests;

public class SellerOnboardingTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public SellerOnboardingTests(ApiFactory factory)
    {
        _factory = factory;
    }

    private static async Task<JsonElement> ApplyAsync(HttpClient client, string businessName)
    {
        HttpResponseMessage res = await client.PostAsJsonAsync("/api/sellers/apply", new { businessName });
        res.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement;
    }

    [Fact]
    public async Task Apply_WhenMarketplaceGated_ReturnsNotFound()
    {
        // Override the marketplace flag off for this client only.
        using var gated = _factory.WithWebHostBuilder(b =>
            b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(
                new Dictionary<string, string?> { ["Marketplace:Enabled"] = "false" })));
        HttpClient client = gated.CreateClient();
        await RegisterAndLogin(client, "seller-gated@test.com");

        HttpResponseMessage res = await client.PostAsJsonAsync("/api/sellers/apply", new { businessName = "Gated Vintage" });

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task Apply_CreatesAppliedSellerWithSlug()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAndLogin(client, "seller-apply@test.com");

        JsonElement seller = await ApplyAsync(client, "Rose & Thorn Vintage");

        Assert.Equal("Applied", seller.GetProperty("approvalStatus").GetString());
        Assert.Equal("rose-thorn-vintage", seller.GetProperty("slug").GetString());
        Assert.False(seller.GetProperty("isHouse").GetBoolean());
    }

    [Fact]
    public async Task Apply_Twice_ReturnsSameSeller()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAndLogin(client, "seller-twice@test.com");

        JsonElement first = await ApplyAsync(client, "Twice Vintage");
        JsonElement second = await ApplyAsync(client, "Something Else Entirely");

        Assert.Equal(first.GetProperty("id").GetGuid(), second.GetProperty("id").GetGuid());
        // The second application must not overwrite the first business name.
        Assert.Equal("Twice Vintage", second.GetProperty("businessName").GetString());
    }

    [Fact]
    public async Task Profile_IsHiddenUntilApproved_ThenVisible()
    {
        HttpClient seller = _factory.CreateClient();
        await RegisterAndLogin(seller, "seller-profile@test.com");
        JsonElement applied = await ApplyAsync(seller, "Hidden Then Seen");
        string slug = applied.GetProperty("slug").GetString()!;
        Guid sellerId = applied.GetProperty("id").GetGuid();

        // Public profile 404s while only Applied.
        HttpClient anon = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await anon.GetAsync($"/api/sellers/{slug}")).StatusCode);

        // Admin approves.
        HttpClient admin = _factory.CreateClient();
        await RegisterAdmin(admin, _factory, "seller-profile-admin@test.com");
        HttpResponseMessage approve = await admin.PostAsync($"/api/sellers/admin/{sellerId}/approve", null);
        approve.EnsureSuccessStatusCode();

        // Now the public profile is visible.
        HttpResponseMessage profile = await anon.GetAsync($"/api/sellers/{slug}");
        Assert.Equal(HttpStatusCode.OK, profile.StatusCode);
        JsonElement body = JsonDocument.Parse(await profile.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("Approved", body.GetProperty("approvalStatus").GetString());
    }

    [Fact]
    public async Task Approve_GrantsSellerRoleToOwner()
    {
        HttpClient seller = _factory.CreateClient();
        var (_, auth) = await RegisterAndLogin(seller, "seller-role@test.com");
        JsonElement applied = await ApplyAsync(seller, "Role Grant Vintage");
        Guid sellerId = applied.GetProperty("id").GetGuid();

        HttpClient admin = _factory.CreateClient();
        await RegisterAdmin(admin, _factory, "seller-role-admin@test.com");
        (await admin.PostAsync($"/api/sellers/admin/{sellerId}/approve", null)).EnsureSuccessStatusCode();

        // Re-login the seller: their role should now be Seller.
        HttpClient fresh = _factory.CreateClient();
        HttpResponseMessage login = await fresh.PostAsJsonAsync("/api/auth/login",
            new { email = "seller-role@test.com", password = "TestPass123!" });
        login.EnsureSuccessStatusCode();
        AuthResponse? reAuth = await login.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        Assert.Equal("Seller", reAuth!.User.Role);
    }

    [Fact]
    public async Task Approve_HouseSeller_ReturnsNotFound()
    {
        HttpClient admin = _factory.CreateClient();
        await RegisterAdmin(admin, _factory, "seller-house-admin@test.com");

        // The well-known house seller must not be moderatable.
        HttpResponseMessage res = await admin.PostAsync(
            "/api/sellers/admin/5e11e400-0000-0000-0000-000000000001/approve", null);

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task AdminList_FiltersByStatus()
    {
        HttpClient seller = _factory.CreateClient();
        await RegisterAndLogin(seller, "seller-list@test.com");
        await ApplyAsync(seller, "Listed Vintage");

        HttpClient admin = _factory.CreateClient();
        await RegisterAdmin(admin, _factory, "seller-list-admin@test.com");

        HttpResponseMessage res = await admin.GetAsync("/api/sellers/admin/all?status=Applied");
        res.EnsureSuccessStatusCode();
        JsonElement list = JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement;
        Assert.True(list.GetArrayLength() >= 1);
        foreach (JsonElement s in list.EnumerateArray())
        {
            Assert.Equal("Applied", s.GetProperty("approvalStatus").GetString());
        }
    }
}
