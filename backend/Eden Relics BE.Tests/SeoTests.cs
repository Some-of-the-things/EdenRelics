using System.Net;
using System.Net.Http.Json;
using static Eden_Relics_BE.Tests.Helpers;

namespace Eden_Relics_BE.Tests;

public class SeoTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public SeoTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetKeywords_AsAdmin_ReturnsEmptyList()
    {
        var client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "seo-getkw@test.com");

        var keywords = await client.GetFromJsonAsync<List<KeywordResponse>>("/api/seo/keywords", JsonOptions);
        Assert.NotNull(keywords);
    }

    [Fact]
    public async Task AddKeyword_AsAdmin_ReturnsCreatedKeyword()
    {
        var client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "seo-addkw@test.com");

        var response = await client.PostAsJsonAsync("/api/seo/keywords", new
        {
            keyword = "vintage dresses uk",
            pageUrl = "https://edenrelics.co.uk",
            position = 15,
            notes = "Main keyword"
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var keyword = await response.Content.ReadFromJsonAsync<KeywordResponse>(JsonOptions);
        Assert.NotNull(keyword);
        Assert.Equal("vintage dresses uk", keyword.Keyword);
        Assert.Equal("https://edenrelics.co.uk", keyword.PageUrl);
        Assert.Equal(15, keyword.LastPosition);
        Assert.Equal("Main keyword", keyword.Notes);
        Assert.NotNull(keyword.LastCheckedUtc);
    }

    [Fact]
    public async Task UpdateKeyword_AsAdmin_ModifiesKeyword()
    {
        var client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "seo-updatekw@test.com");

        var createResponse = await client.PostAsJsonAsync("/api/seo/keywords", new
        {
            keyword = "original keyword",
            pageUrl = "https://edenrelics.co.uk",
            position = 20
        });
        var created = await createResponse.Content.ReadFromJsonAsync<KeywordResponse>(JsonOptions);

        var updateResponse = await client.PutAsJsonAsync($"/api/seo/keywords/{created!.Id}", new
        {
            keyword = "updated keyword",
            position = 5,
            notes = "Improved!"
        });
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var updated = await updateResponse.Content.ReadFromJsonAsync<KeywordResponse>(JsonOptions);
        Assert.Equal("updated keyword", updated!.Keyword);
        Assert.Equal(5, updated.LastPosition);
        Assert.Equal("Improved!", updated.Notes);
    }

    [Fact]
    public async Task UpdateKeyword_NonExistent_Returns404()
    {
        var client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "seo-updatekw-404@test.com");

        var response = await client.PutAsJsonAsync($"/api/seo/keywords/{Guid.Empty}", new
        {
            keyword = "nope"
        });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteKeyword_AsAdmin_Returns204()
    {
        var client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "seo-delkw@test.com");

        var createResponse = await client.PostAsJsonAsync("/api/seo/keywords", new
        {
            keyword = "to delete keyword",
            pageUrl = "https://edenrelics.co.uk",
            position = 30
        });
        var created = await createResponse.Content.ReadFromJsonAsync<KeywordResponse>(JsonOptions);

        var response = await client.DeleteAsync($"/api/seo/keywords/{created!.Id}");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // Verify deleted
        var keywords = await client.GetFromJsonAsync<List<KeywordResponse>>("/api/seo/keywords", JsonOptions);
        Assert.DoesNotContain(keywords!, k => k.Id == created.Id);
    }

    [Fact]
    public async Task DeleteKeyword_NonExistent_Returns404()
    {
        var client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "seo-delkw-404@test.com");

        var response = await client.DeleteAsync($"/api/seo/keywords/{Guid.Empty}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AllEndpoints_Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/seo/keywords")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/seo/keywords", new { keyword = "x", pageUrl = "x" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.DeleteAsync($"/api/seo/keywords/{Guid.NewGuid()}")).StatusCode);
    }

    [Fact]
    public async Task AllEndpoints_AsCustomer_Returns403()
    {
        var client = _factory.CreateClient();
        await RegisterAndLogin(client, "seo-customer@test.com");

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/seo/keywords")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/seo/keywords", new { keyword = "x", pageUrl = "x" })).StatusCode);
    }

    [Fact]
    public async Task Analyse_MissingUrl_Returns400()
    {
        var client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "seo-analyse-empty@test.com");

        var response = await client.PostAsJsonAsync("/api/seo/analyse", new
        {
            url = ""
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private record KeywordResponse(Guid Id, string Keyword, string PageUrl, int? LastPosition, DateTime? LastCheckedUtc, string? Notes);
}
