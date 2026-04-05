using System.Net;
using System.Net.Http.Json;
using static Eden_Relics_BE.Tests.Helpers;

namespace Eden_Relics_BE.Tests;

public class ContentLocaleTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public ContentLocaleTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetAll_WithoutLocale_ReturnsEnglishDefaults()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "content-en-default@test.com");

        // Ensure seed value is set (other tests in this class may overwrite it)
        await client.PutAsJsonAsync("/api/content", new Dictionary<string, string>
        {
            ["home.hero.title"] = "Curated Vintage"
        });

        Dictionary<string, string>? content = await client.GetFromJsonAsync<Dictionary<string, string>>("/api/content", JsonOptions);
        Assert.NotNull(content);
        Assert.True(content.ContainsKey("home.hero.title"));
        Assert.Equal("Curated Vintage", content["home.hero.title"]);
    }

    [Fact]
    public async Task GetAll_WithEnLocale_ReturnsEnglishDefaults()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "content-en-locale@test.com");

        // Ensure seed value is set (other tests in this class may overwrite it)
        await client.PutAsJsonAsync("/api/content", new Dictionary<string, string>
        {
            ["home.hero.title"] = "Curated Vintage"
        });

        Dictionary<string, string>? content = await client.GetFromJsonAsync<Dictionary<string, string>>("/api/content?locale=en", JsonOptions);
        Assert.NotNull(content);
        Assert.True(content.ContainsKey("home.hero.title"));
        Assert.Equal("Curated Vintage", content["home.hero.title"]);
    }

    [Fact]
    public async Task GetAll_WithUnsupportedLocale_FallsBackToEnglish()
    {
        HttpClient client = _factory.CreateClient();
        Dictionary<string, string>? content = await client.GetFromJsonAsync<Dictionary<string, string>>("/api/content?locale=xx", JsonOptions);
        Assert.NotNull(content);
        // Should fall back to English since "xx" is not a supported locale
        Assert.True(content.ContainsKey("home.hero.title"));
    }

    [Fact]
    public async Task GetAll_WithLocale_ReturnsTranslatedContentIfAvailable()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "content-locale-test@test.com");

        // Save content with a French translation manually
        await client.PutAsJsonAsync("/api/content", new Dictionary<string, string>
        {
            ["home.hero.title"] = "Curated Vintage",
            ["fr.home.hero.title"] = "Vintage Soigné"
        });

        // Request French locale
        Dictionary<string, string>? frContent = await client.GetFromJsonAsync<Dictionary<string, string>>("/api/content?locale=fr", JsonOptions);
        Assert.NotNull(frContent);
        Assert.Equal("Vintage Soigné", frContent["home.hero.title"]);
    }

    [Fact]
    public async Task GetAll_WithLocale_FallsBackToEnglishForMissingTranslations()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "content-locale-fallback@test.com");

        // Save only English content
        await client.PutAsJsonAsync("/api/content", new Dictionary<string, string>
        {
            ["home.hero.subtitle"] = "Timeless pieces from decades past."
        });

        // Request German locale — no translation exists
        Dictionary<string, string>? deContent = await client.GetFromJsonAsync<Dictionary<string, string>>("/api/content?locale=de", JsonOptions);
        Assert.NotNull(deContent);
        // Should fall back to English
        Assert.Equal("Timeless pieces from decades past.", deContent["home.hero.subtitle"]);
    }

    [Fact]
    public async Task GetAll_DoesNotReturnLocalePrefixedKeys()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "content-no-prefix@test.com");

        await client.PutAsJsonAsync("/api/content", new Dictionary<string, string>
        {
            ["home.hero.title"] = "Test Title",
            ["fr.home.hero.title"] = "Titre Test"
        });

        // Default (English) request should not include fr.* keys
        Dictionary<string, string>? content = await client.GetFromJsonAsync<Dictionary<string, string>>("/api/content", JsonOptions);
        Assert.NotNull(content);
        Assert.False(content.ContainsKey("fr.home.hero.title"));
        Assert.True(content.ContainsKey("home.hero.title"));
    }
}
