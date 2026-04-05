using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using static Eden_Relics_BE.Tests.Helpers;

namespace Eden_Relics_BE.Tests;

public class AdminUsersTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public AdminUsersTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetAll_AsAdmin_ReturnsUsers()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "admin-users-getall@test.com");

        // Register another user so there's more than one
        HttpClient client2 = _factory.CreateClient();
        await RegisterAndLogin(client2, "admin-users-other@test.com");

        HttpResponseMessage response = await client.GetAsync("/api/admin/users");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        string json = await response.Content.ReadAsStringAsync();
        JsonElement users = JsonDocument.Parse(json).RootElement;
        Assert.True(users.GetArrayLength() >= 2);

        // Check that the response contains expected fields
        JsonElement firstUser = users[0];
        Assert.True(firstUser.TryGetProperty("id", out _));
        Assert.True(firstUser.TryGetProperty("email", out _));
        Assert.True(firstUser.TryGetProperty("firstName", out _));
        Assert.True(firstUser.TryGetProperty("role", out _));
        Assert.True(firstUser.TryGetProperty("orderCount", out _));
        Assert.True(firstUser.TryGetProperty("mailingList", out _));
        Assert.True(firstUser.TryGetProperty("favourites", out _));
    }

    [Fact]
    public async Task GetAll_Unauthenticated_Returns401()
    {
        HttpClient client = _factory.CreateClient();
        HttpResponseMessage response = await client.GetAsync("/api/admin/users");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_AsCustomer_Returns403()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAndLogin(client, "admin-users-customer@test.com");
        HttpResponseMessage response = await client.GetAsync("/api/admin/users");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
