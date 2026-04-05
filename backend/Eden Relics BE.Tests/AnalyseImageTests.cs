using System.Net;
using System.Net.Http.Json;
using static Eden_Relics_BE.Tests.Helpers;

namespace Eden_Relics_BE.Tests;

public class AnalyseImageTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public AnalyseImageTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task AnalyseImage_Unauthenticated_Returns401()
    {
        HttpClient client = _factory.CreateClient();
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/products/analyse-image", new
        {
            imageUrl = "https://example.com/image.webp"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AnalyseImage_AsNonAdmin_Returns403()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAndLogin(client, "analyse-user@test.com");
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/products/analyse-image", new
        {
            imageUrl = "https://example.com/image.webp"
        });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AnalyseImage_NoApiKey_Returns503()
    {
        // The test environment has no Anthropic API key configured
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "analyse-admin@test.com");
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/products/analyse-image", new
        {
            imageUrl = "https://example.com/image.webp"
        });
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);

        AnalyseImageError? error = await response.Content.ReadFromJsonAsync<AnalyseImageError>(JsonOptions);
        Assert.NotNull(error);
        Assert.Equal("Anthropic API key not configured.", error.Error);
    }
}
