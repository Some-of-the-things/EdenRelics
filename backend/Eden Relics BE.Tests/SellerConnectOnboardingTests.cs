using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using static Eden_Relics_BE.Tests.Helpers;

namespace Eden_Relics_BE.Tests;

public class SellerConnectOnboardingTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public SellerConnectOnboardingTests(ApiFactory factory)
    {
        _factory = factory;
    }

    /// <summary>An approved seller re-logged-in with the Seller role, NOT yet Connect-onboarded.</summary>
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

        seller.DefaultRequestHeaders.Authorization = null;
        HttpResponseMessage login = await seller.PostAsJsonAsync("/api/auth/login", new { email, password = "TestPass123!" });
        login.EnsureSuccessStatusCode();
        AuthResponse? auth = await login.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        seller.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.Token);
        return (seller, admin, sellerId);
    }

    private static async Task OnboardAsync(HttpClient seller)
    {
        (await seller.PostAsync("/api/sellers/connect/start", null)).EnsureSuccessStatusCode();
        (await seller.PostAsync("/api/sellers/connect/refresh", null)).EnsureSuccessStatusCode();
    }

    private static async Task<Guid> CreateListingAsync(HttpClient seller, string name)
    {
        HttpResponseMessage res = await seller.PostAsJsonAsync("/api/seller-listings", new
        {
            name, description = "A test piece.", price = 100.0, era = "1970s",
            category = "70s", size = "12", condition = "good", imageUrl = "https://example.com/x.webp",
        });
        res.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task ConnectStart_ReturnsOnboardingUrl()
    {
        var (seller, _, _) = await ApprovedSellerAsync("connect-start@test.com", "Connect Vintage");
        HttpResponseMessage res = await seller.PostAsync("/api/sellers/connect/start", null);
        res.EnsureSuccessStatusCode();
        JsonElement body = JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement;
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("url").GetString()));
    }

    [Fact]
    public async Task ConnectRefresh_MarksOnboardingComplete()
    {
        var (seller, _, _) = await ApprovedSellerAsync("connect-refresh@test.com", "Refresh Vintage");
        (await seller.PostAsync("/api/sellers/connect/start", null)).EnsureSuccessStatusCode();
        HttpResponseMessage res = await seller.PostAsync("/api/sellers/connect/refresh", null);
        res.EnsureSuccessStatusCode();
        JsonElement body = JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement;
        Assert.True(body.GetProperty("onboardingComplete").GetBoolean());
    }

    [Fact]
    public async Task NonSeller_CannotStartConnect()
    {
        HttpClient customer = _factory.CreateClient();
        await RegisterAndLogin(customer, "connect-customer@test.com");
        HttpResponseMessage res = await customer.PostAsync("/api/sellers/connect/start", null);
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task ListingApproval_BlockedUntilSellerOnboarded()
    {
        var (seller, admin, _) = await ApprovedSellerAsync("connect-gate@test.com", "Gate Vintage");
        Guid listingId = await CreateListingAsync(seller, "Payment-Gated Listing");

        // Not payment-ready yet — approval must be refused.
        HttpResponseMessage blocked = await admin.PostAsync($"/api/seller-listings/admin/{listingId}/approve", null);
        Assert.Equal(HttpStatusCode.NotFound, blocked.StatusCode);

        // Complete onboarding, then approval succeeds and the listing goes live.
        await OnboardAsync(seller);
        HttpResponseMessage ok = await admin.PostAsync($"/api/seller-listings/admin/{listingId}/approve", null);
        ok.EnsureSuccessStatusCode();
        JsonElement body = JsonDocument.Parse(await ok.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("Live", body.GetProperty("status").GetString());
        Assert.Equal("Approved", body.GetProperty("moderationStatus").GetString());
    }
}
