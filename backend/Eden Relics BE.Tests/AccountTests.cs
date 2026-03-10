using System.Net;
using System.Net.Http.Json;
using static Eden_Relics_BE.Tests.Helpers;

namespace Eden_Relics_BE.Tests;

public class AccountTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public AccountTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetProfile_Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/account/profile");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetProfile_Authenticated_ReturnsProfile()
    {
        var client = _factory.CreateClient();
        await RegisterAndLogin(client, "profile@test.com");

        var profile = await client.GetFromJsonAsync<ProfileResponse>("/api/account/profile", JsonOptions);
        Assert.NotNull(profile);
        Assert.Equal("profile@test.com", profile.Email);
        Assert.Equal("Test", profile.FirstName);
        Assert.False(profile.MfaEnabled);
        Assert.False(profile.EmailVerified);
    }

    [Fact]
    public async Task UpdateProfile_ChangesName()
    {
        var client = _factory.CreateClient();
        await RegisterAndLogin(client, "updatename@test.com");

        var response = await client.PutAsJsonAsync("/api/account/profile", new
        {
            firstName = "Updated",
            lastName = "Name"
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var profile = await response.Content.ReadFromJsonAsync<ProfileResponse>(JsonOptions);
        Assert.Equal("Updated", profile!.FirstName);
        Assert.Equal("Name", profile.LastName);
    }

    [Fact]
    public async Task UpdateDeliveryAddress_SavesAddress()
    {
        var client = _factory.CreateClient();
        await RegisterAndLogin(client, "delivery@test.com");

        var response = await client.PutAsJsonAsync("/api/account/delivery-address", new
        {
            addressLine1 = "123 Test Street",
            addressLine2 = "Flat 4",
            city = "London",
            county = "Greater London",
            postcode = "SW1A 1AA",
            country = "UK"
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var profile = await response.Content.ReadFromJsonAsync<ProfileResponse>(JsonOptions);
        Assert.Equal("123 Test Street", profile!.DeliveryAddress.AddressLine1);
        Assert.Equal("London", profile.DeliveryAddress.City);
        Assert.Equal("SW1A 1AA", profile.DeliveryAddress.Postcode);
    }

    [Fact]
    public async Task UpdateBillingAddress_SavesAddress()
    {
        var client = _factory.CreateClient();
        await RegisterAndLogin(client, "billing@test.com");

        var response = await client.PutAsJsonAsync("/api/account/billing-address", new
        {
            addressLine1 = "456 Billing Rd",
            city = "Manchester",
            postcode = "M1 1AA",
            country = "UK"
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var profile = await response.Content.ReadFromJsonAsync<ProfileResponse>(JsonOptions);
        Assert.Equal("456 Billing Rd", profile!.BillingAddress.AddressLine1);
        Assert.Equal("Manchester", profile.BillingAddress.City);
    }

    [Fact]
    public async Task UpdatePayment_SavesCardInfo()
    {
        var client = _factory.CreateClient();
        await RegisterAndLogin(client, "payment@test.com");

        var response = await client.PutAsJsonAsync("/api/account/payment", new
        {
            cardholderName = "Test User",
            cardLast4 = "4242",
            cardBrand = "Visa",
            expiryMonth = 12,
            expiryYear = 2027
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var profile = await response.Content.ReadFromJsonAsync<ProfileResponse>(JsonOptions);
        Assert.NotNull(profile!.Payment);
        Assert.Equal("4242", profile.Payment.CardLast4);
        Assert.Equal("Visa", profile.Payment.CardBrand);
    }

    [Fact]
    public async Task ChangePassword_ValidCurrent_Succeeds()
    {
        var client = _factory.CreateClient();
        await RegisterAndLogin(client, "changepw@test.com");

        var response = await client.PostAsJsonAsync("/api/account/change-password", new
        {
            currentPassword = "TestPass123!",
            newPassword = "NewPass456!"
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Verify can login with new password
        var client2 = _factory.CreateClient();
        var loginResponse = await client2.PostAsJsonAsync("/api/auth/login", new
        {
            email = "changepw@test.com",
            password = "NewPass456!"
        });
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
    }

    [Fact]
    public async Task ChangePassword_WrongCurrent_Returns400()
    {
        var client = _factory.CreateClient();
        await RegisterAndLogin(client, "wrongcurrent@test.com");

        var response = await client.PostAsJsonAsync("/api/account/change-password", new
        {
            currentPassword = "WrongPassword!",
            newPassword = "NewPass456!"
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task MfaSetup_ReturnsSecretAndQrUri()
    {
        var client = _factory.CreateClient();
        await RegisterAndLogin(client, "mfa@test.com");

        var response = await client.PostAsync("/api/account/mfa/setup", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var setup = await response.Content.ReadFromJsonAsync<MfaSetupResponse>(JsonOptions);
        Assert.NotNull(setup);
        Assert.False(string.IsNullOrEmpty(setup.Secret));
        Assert.Contains("otpauth://totp/", setup.QrUri);
    }
}
