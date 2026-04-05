using System.Net;
using System.Net.Http.Json;
using static Eden_Relics_BE.Tests.Helpers;

namespace Eden_Relics_BE.Tests;

public class PasskeyTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public PasskeyTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task RegisterOptions_Unauthenticated_Returns401()
    {
        HttpClient client = _factory.CreateClient();
        HttpResponseMessage response = await client.PostAsync("/api/passkey/register-options", null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RegisterOptions_Authenticated_ReturnsOptions()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAndLogin(client, "passkey@test.com");

        HttpResponseMessage response = await client.PostAsync("/api/passkey/register-options", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        Assert.Contains("challenge", body);
        Assert.Contains("Eden Relics", body);
    }

    [Fact]
    public async Task LoginOptions_ReturnsOptions()
    {
        HttpClient client = _factory.CreateClient();
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/passkey/login-options", new
        {
            email = "anyone@test.com"
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        Assert.Contains("sessionId", body);
        Assert.Contains("options", body);
    }

    [Fact]
    public async Task GetCredentials_Unauthenticated_Returns401()
    {
        HttpClient client = _factory.CreateClient();
        HttpResponseMessage response = await client.GetAsync("/api/passkey/credentials");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetCredentials_Authenticated_ReturnsEmptyList()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAndLogin(client, "passkeycreds@test.com");

        HttpResponseMessage response = await client.GetAsync("/api/passkey/credentials");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        Assert.Equal("[]", body);
    }

    [Fact]
    public async Task DeleteCredential_NonExistent_Returns404()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAndLogin(client, "deletecred@test.com");

        HttpResponseMessage response = await client.DeleteAsync($"/api/passkey/credentials/{Guid.Empty}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task RenameCredential_NonExistent_Returns404()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAndLogin(client, "renamecred@test.com");

        HttpResponseMessage response = await client.PutAsJsonAsync($"/api/passkey/credentials/{Guid.Empty}", new
        {
            nickname = "My Key"
        });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
