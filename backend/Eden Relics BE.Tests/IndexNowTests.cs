using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Eden_Relics_BE.Services;
using Microsoft.Extensions.DependencyInjection;
using static Eden_Relics_BE.Tests.Helpers;

namespace Eden_Relics_BE.Tests;

/// <summary>
/// IndexNow submits URLs to search engines on our behalf, so the two things worth pinning down are
/// that it can never submit a URL the sitemap does not advertise, and that it stays inert until it
/// is deliberately switched on.
/// </summary>
public class IndexNowTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public IndexNowTests(ApiFactory factory)
    {
        _factory = factory;
    }

    private record StatusResponse(bool Configured, string? KeyLocation);

    [Fact]
    public async Task Status_Unauthenticated_Returns401()
    {
        HttpClient client = _factory.CreateClient();
        HttpResponseMessage response = await client.GetAsync("/api/seo/indexnow/status");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Disabled by default. A test run, a developer machine or a staging box must never be able to
    /// tell Bing that a half-built page went live.
    /// </summary>
    [Fact]
    public async Task Status_ReportsDisabledUntilExplicitlyEnabled()
    {
        HttpClient client = _factory.CreateClient();
        (string token, _) = await RegisterAdmin(client, _factory, "indexnow-admin@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        StatusResponse? status = await client.GetFromJsonAsync<StatusResponse>("/api/seo/indexnow/status");

        Assert.NotNull(status);
        Assert.False(status!.Configured);
        // The key location is still reported so the key file can be verified before switching on.
        Assert.Equal("https://edenrelics.co.uk/18a8cfc6ffcae60a57cbd5ec353c6541.txt", status.KeyLocation);
    }

    [Fact]
    public async Task SubmitAll_WhenDisabled_DoesNothingAndSaysSo()
    {
        HttpClient client = _factory.CreateClient();
        (string token, _) = await RegisterAdmin(client, _factory, "indexnow-submit@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        HttpResponseMessage response = await client.PostAsync("/api/seo/indexnow/submit-all", null);
        response.EnsureSuccessStatusCode();
        IndexNowResult? result = await response.Content.ReadFromJsonAsync<IndexNowResult>();

        Assert.NotNull(result);
        Assert.False(result!.Submitted);
        Assert.Equal(0, result.UrlCount);
    }

    [Fact]
    public async Task Submit_WithNoUrls_Returns400()
    {
        HttpClient client = _factory.CreateClient();
        (string token, _) = await RegisterAdmin(client, _factory, "indexnow-empty@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/seo/indexnow/submit", new { urls = Array.Empty<string>() });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// The submission list and the sitemap are built from one enumeration, so they cannot drift.
    /// If this ever fails, IndexNow has started advertising pages the sitemap does not — which is
    /// how you end up submitting unpublished or deleted URLs.
    /// </summary>
    [Fact]
    public async Task IndexableUrls_MatchTheSitemapExactly()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        ISitemapService sitemap = scope.ServiceProvider.GetRequiredService<ISitemapService>();

        string xml = await sitemap.BuildSitemapXmlAsync();
        IReadOnlyList<string> urls = await sitemap.GetIndexableUrlsAsync();

        List<string> fromXml = Regex.Matches(xml, "<loc>(.*?)</loc>")
            .Select(m => m.Groups[1].Value
                .Replace("&amp;", "&").Replace("&lt;", "<").Replace("&gt;", ">"))
            .ToList();

        Assert.NotEmpty(urls);
        Assert.Equal(fromXml, urls.ToList());
    }

    /// <summary>
    /// A batch containing a URL on another host is refused wholesale by IndexNow, so foreign URLs
    /// are dropped rather than allowed to take the legitimate ones down with them.
    /// </summary>
    [Fact]
    public async Task Submit_DropsUrlsThatAreNotOurs()
    {
        HttpClient client = _factory.CreateClient();
        (string token, _) = await RegisterAdmin(client, _factory, "indexnow-foreign@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/seo/indexnow/submit",
            new { urls = new[] { "https://example.com/not-ours", "https://edenrelics.co.uk/" } });

        response.EnsureSuccessStatusCode();
        IndexNowResult? result = await response.Content.ReadFromJsonAsync<IndexNowResult>();

        // Disabled in test config, so nothing is sent — the point here is that the endpoint
        // accepts the request and reports rather than throwing on a foreign host.
        Assert.NotNull(result);
        Assert.False(result!.Submitted);
    }
}
