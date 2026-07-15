using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using static Eden_Relics_BE.Tests.Helpers;

namespace Eden_Relics_BE.Tests;

public class SellerListingModerationTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public SellerListingModerationTests(ApiFactory factory)
    {
        _factory = factory;
    }

    /// <summary>Registers a user, applies as a seller, has an admin approve them, and re-logs in so
    /// the seller's token carries the Seller role. Returns the seller client, an admin client, and id.</summary>
    private async Task<(HttpClient Seller, HttpClient Admin, Guid SellerId)> ApprovedSellerAsync(string email, string business)
    {
        HttpClient seller = _factory.CreateClient();
        await RegisterAndLogin(seller, email);
        HttpResponseMessage apply = await seller.PostAsJsonAsync("/api/sellers/apply", new { businessName = business });
        apply.EnsureSuccessStatusCode();
        Guid sellerId = JsonDocument.Parse(await apply.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();

        HttpClient admin = _factory.CreateClient();
        await RegisterAdmin(admin, _factory, email.Replace("@", "-admin@"));
        (await admin.PostAsync($"/api/sellers/admin/{sellerId}/approve", null)).EnsureSuccessStatusCode();

        // Re-login the seller so the JWT carries the freshly granted Seller role.
        seller.DefaultRequestHeaders.Authorization = null;
        HttpResponseMessage login = await seller.PostAsJsonAsync("/api/auth/login", new { email, password = "TestPass123!" });
        login.EnsureSuccessStatusCode();
        AuthResponse? auth = await login.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        seller.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.Token);

        // Complete Stripe Connect onboarding (faked) so the seller is payment-ready — a listing
        // can't be approved/go live until this is done.
        (await seller.PostAsync("/api/sellers/connect/start", null)).EnsureSuccessStatusCode();
        (await seller.PostAsync("/api/sellers/connect/refresh", null)).EnsureSuccessStatusCode();

        return (seller, admin, sellerId);
    }

    private static async Task<JsonElement> CreateListingAsync(HttpClient seller, string name)
    {
        HttpResponseMessage res = await seller.PostAsJsonAsync("/api/seller-listings", new
        {
            name,
            description = "A lovely vintage piece, for testing.",
            price = 120.0,
            era = "1970s",
            category = "70s",
            size = "12",
            condition = "good",
            imageUrl = "https://example.com/piece.webp",
        });
        res.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement;
    }

    private async Task<bool> IsPubliclyListedAsync(Guid productId)
    {
        HttpClient anon = _factory.CreateClient();
        HttpResponseMessage res = await anon.GetAsync("/api/products");
        res.EnsureSuccessStatusCode();
        JsonElement arr = JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement;
        foreach (JsonElement p in arr.EnumerateArray())
        {
            if (p.GetProperty("id").GetGuid() == productId)
            {
                return true;
            }
        }
        return false;
    }

    [Fact]
    public async Task SellerListing_EntersPendingAndIsHiddenFromPublic()
    {
        var (seller, _, _) = await ApprovedSellerAsync("listing-pending@test.com", "Pending Vintage");
        JsonElement listing = await CreateListingAsync(seller, "Hidden Pending Dress");

        Assert.Equal("Stock", listing.GetProperty("status").GetString());
        Assert.Equal("PendingReview", listing.GetProperty("moderationStatus").GetString());
        Assert.False(await IsPubliclyListedAsync(listing.GetProperty("id").GetGuid()));
    }

    [Fact]
    public async Task NonSeller_CannotCreateListing()
    {
        HttpClient customer = _factory.CreateClient();
        await RegisterAndLogin(customer, "listing-customer@test.com"); // plain Customer role

        HttpResponseMessage res = await customer.PostAsJsonAsync("/api/seller-listings", new
        {
            name = "Sneaky", description = "x", price = 10.0, era = "1970s",
            category = "70s", size = "12", condition = "good", imageUrl = "https://x/y.webp",
        });

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task AdminApprove_MakesListingLiveAndPublic()
    {
        var (seller, admin, _) = await ApprovedSellerAsync("listing-approve@test.com", "Approve Vintage");
        JsonElement listing = await CreateListingAsync(seller, "Soon To Be Live Dress");
        Guid id = listing.GetProperty("id").GetGuid();

        Assert.False(await IsPubliclyListedAsync(id));

        HttpResponseMessage approve = await admin.PostAsync($"/api/seller-listings/admin/{id}/approve", null);
        approve.EnsureSuccessStatusCode();
        JsonElement result = JsonDocument.Parse(await approve.Content.ReadAsStringAsync()).RootElement;

        Assert.Equal("Live", result.GetProperty("status").GetString());
        Assert.Equal("Approved", result.GetProperty("moderationStatus").GetString());
        Assert.True(await IsPubliclyListedAsync(id));
    }

    [Fact]
    public async Task AdminReject_KeepsListingHidden()
    {
        var (seller, admin, _) = await ApprovedSellerAsync("listing-reject@test.com", "Reject Vintage");
        JsonElement listing = await CreateListingAsync(seller, "Will Be Rejected Dress");
        Guid id = listing.GetProperty("id").GetGuid();

        HttpResponseMessage reject = await admin.PostAsJsonAsync(
            $"/api/seller-listings/admin/{id}/reject", new { note = "Photos too blurry." });
        reject.EnsureSuccessStatusCode();
        JsonElement result = JsonDocument.Parse(await reject.Content.ReadAsStringAsync()).RootElement;

        Assert.Equal("Rejected", result.GetProperty("moderationStatus").GetString());
        Assert.Equal("Stock", result.GetProperty("status").GetString());
        Assert.Equal("Photos too blurry.", result.GetProperty("moderationNote").GetString());
        Assert.False(await IsPubliclyListedAsync(id));
    }

    [Fact]
    public async Task SellerPublicProducts_ShowsOnlyApprovedLiveListings()
    {
        var (seller, admin, _) = await ApprovedSellerAsync("listing-profile@test.com", "Profile Vintage");
        JsonElement me = JsonDocument.Parse(await (await seller.GetAsync("/api/sellers/me")).Content.ReadAsStringAsync()).RootElement;
        string slug = me.GetProperty("slug").GetString()!;

        JsonElement live = await CreateListingAsync(seller, "Live Profile Piece");
        JsonElement pending = await CreateListingAsync(seller, "Pending Profile Piece");
        Guid liveId = live.GetProperty("id").GetGuid();
        Guid pendingId = pending.GetProperty("id").GetGuid();

        (await admin.PostAsync($"/api/seller-listings/admin/{liveId}/approve", null)).EnsureSuccessStatusCode();

        HttpClient anon = _factory.CreateClient();
        HttpResponseMessage res = await anon.GetAsync($"/api/sellers/{slug}/products");
        res.EnsureSuccessStatusCode();
        JsonElement arr = JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement;

        List<Guid> ids = arr.EnumerateArray().Select(p => p.GetProperty("id").GetGuid()).ToList();
        Assert.Contains(liveId, ids);
        Assert.DoesNotContain(pendingId, ids);
    }

    [Fact]
    public async Task AdminQueue_ListsPendingListings()
    {
        var (seller, admin, _) = await ApprovedSellerAsync("listing-queue@test.com", "Queue Vintage");
        JsonElement listing = await CreateListingAsync(seller, "Queued Dress");
        Guid id = listing.GetProperty("id").GetGuid();

        HttpResponseMessage res = await admin.GetAsync("/api/seller-listings/admin?status=PendingReview");
        res.EnsureSuccessStatusCode();
        JsonElement arr = JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement;

        bool found = false;
        foreach (JsonElement p in arr.EnumerateArray())
        {
            if (p.GetProperty("id").GetGuid() == id)
            {
                found = true;
                Assert.Equal("PendingReview", p.GetProperty("moderationStatus").GetString());
            }
        }
        Assert.True(found);
    }
}
