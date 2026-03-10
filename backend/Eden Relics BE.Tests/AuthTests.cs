using System.Net;
using System.Net.Http.Json;
using static Eden_Relics_BE.Tests.Helpers;

namespace Eden_Relics_BE.Tests;

public class AuthTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public AuthTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Register_ReturnsTokenAndUser()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email = "register@test.com",
            password = "TestPass123!",
            firstName = "John",
            lastName = "Doe"
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        Assert.NotNull(auth);
        Assert.False(string.IsNullOrEmpty(auth.Token));
        Assert.Equal("register@test.com", auth.User.Email);
        Assert.Equal("John", auth.User.FirstName);
        Assert.Equal("Customer", auth.User.Role);
        Assert.False(auth.User.EmailVerified);
    }

    [Fact]
    public async Task Register_DuplicateEmail_Returns409()
    {
        var client = _factory.CreateClient();
        var body = new
        {
            email = "dup@test.com",
            password = "TestPass123!",
            firstName = "A",
            lastName = "B"
        };
        await client.PostAsJsonAsync("/api/auth/register", body);
        var response = await client.PostAsJsonAsync("/api/auth/register", body);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Login_ValidCredentials_ReturnsToken()
    {
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/register", new
        {
            email = "login@test.com",
            password = "TestPass123!",
            firstName = "A",
            lastName = "B"
        });

        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "login@test.com",
            password = "TestPass123!"
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        Assert.NotNull(auth);
        Assert.False(string.IsNullOrEmpty(auth.Token));
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401()
    {
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/register", new
        {
            email = "wrongpw@test.com",
            password = "TestPass123!",
            firstName = "A",
            lastName = "B"
        });

        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "wrongpw@test.com",
            password = "WrongPassword!"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_NonExistentUser_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "nobody@test.com",
            password = "TestPass123!"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ForgotPassword_ReturnsOk_EvenForUnknownEmail()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/forgot-password", new
        {
            email = "unknown@test.com"
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task VerifyEmail_InvalidToken_Returns400()
    {
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/register", new
        {
            email = "verify@test.com",
            password = "TestPass123!",
            firstName = "A",
            lastName = "B"
        });

        var response = await client.PostAsJsonAsync("/api/auth/verify-email", new
        {
            email = "verify@test.com",
            token = "wrong-token"
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ResendVerification_Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsync("/api/auth/resend-verification", null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ResendVerification_Authenticated_ReturnsOk()
    {
        var client = _factory.CreateClient();
        await RegisterAndLogin(client, "resend@test.com");

        var response = await client.PostAsync("/api/auth/resend-verification", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
