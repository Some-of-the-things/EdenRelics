using System.Net;
using System.Net.Http.Json;
using static Eden_Relics_BE.Tests.Helpers;

namespace Eden_Relics_BE.Tests;

public class ContentTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public ContentTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetAll_ReturnsDefaults()
    {
        HttpClient client = _factory.CreateClient();
        Dictionary<string, string>? content = await client.GetFromJsonAsync<Dictionary<string, string>>("/api/content", JsonOptions);
        Assert.NotNull(content);
        Assert.True(content.ContainsKey("home.hero.title"));
        Assert.Equal("Curated Vintage", content["home.hero.title"]);
        Assert.True(content.ContainsKey("footer.contact.email"));
        Assert.Equal("edenrelics@dcp-net.com", content["footer.contact.email"]);
    }

    [Fact]
    public async Task UpdateAll_AsAdmin_SavesAndReturnsContent()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "content-update@test.com");

        HttpResponseMessage response = await client.PutAsJsonAsync("/api/content", new Dictionary<string, string>
        {
            ["home.hero.title"] = "Updated Title",
            ["custom.key"] = "Custom Value"
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        Dictionary<string, string>? content = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>(JsonOptions);
        Assert.NotNull(content);
        Assert.Equal("Updated Title", content["home.hero.title"]);
        Assert.Equal("Custom Value", content["custom.key"]);
        // Defaults should still be present for keys not overridden
        Assert.True(content.ContainsKey("footer.contact.email"));
    }

    [Fact]
    public async Task UpdateAll_Unauthenticated_Returns401()
    {
        HttpClient client = _factory.CreateClient();
        HttpResponseMessage response = await client.PutAsJsonAsync("/api/content", new Dictionary<string, string>
        {
            ["home.hero.title"] = "Hacked"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UpdateAll_AsCustomer_Returns403()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAndLogin(client, "content-customer@test.com");

        HttpResponseMessage response = await client.PutAsJsonAsync("/api/content", new Dictionary<string, string>
        {
            ["home.hero.title"] = "Hacked"
        });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_AfterUpdate_ReturnsMergedContent()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "content-merged@test.com");

        await client.PutAsJsonAsync("/api/content", new Dictionary<string, string>
        {
            ["home.hero.eyebrow"] = "New Eyebrow"
        });

        // Fetch with public client
        HttpClient publicClient = _factory.CreateClient();
        Dictionary<string, string>? content = await publicClient.GetFromJsonAsync<Dictionary<string, string>>("/api/content", JsonOptions);
        Assert.NotNull(content);
        Assert.Equal("New Eyebrow", content["home.hero.eyebrow"]);
        // Other defaults still present
        Assert.True(content.ContainsKey("home.about.title"));
    }
}
