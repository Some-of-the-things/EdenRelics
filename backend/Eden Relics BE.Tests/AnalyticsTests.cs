using System.Net;
using System.Net.Http.Json;
using static Eden_Relics_BE.Tests.Helpers;

namespace Eden_Relics_BE.Tests;

public class AnalyticsTests : IClassFixture<ApiFactory>
{
    private const string Secret = "test-analytics-secret";
    private const string HumanUa =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

    private readonly ApiFactory _factory;

    public AnalyticsTests(ApiFactory factory)
    {
        _factory = factory;
    }

    private static HttpRequestMessage Beacon(string path, string? ua, string? secret, string? org = null, string country = "GB")
    {
        HttpRequestMessage req = new(HttpMethod.Post, "/api/analytics/pageview")
        {
            Content = JsonContent.Create(new { path, country, userAgent = ua, asOrganization = org }),
        };
        if (secret is not null)
        {
            req.Headers.Add("X-Analytics-Secret", secret);
        }
        return req;
    }

    [Fact]
    public async Task PageView_WithoutSecret_Returns401()
    {
        HttpClient client = _factory.CreateClient();
        HttpResponseMessage resp = await client.SendAsync(Beacon("/no-secret", HumanUa, secret: null));
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task PageView_WrongSecret_Returns401()
    {
        HttpClient client = _factory.CreateClient();
        HttpResponseMessage resp = await client.SendAsync(Beacon("/wrong-secret", HumanUa, secret: "nope"));
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task PageView_ValidSecret_Returns204()
    {
        HttpClient client = _factory.CreateClient();
        HttpResponseMessage resp = await client.SendAsync(Beacon("/valid-secret", HumanUa, Secret));
        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);
    }

    [Fact]
    public async Task PageViews_AggregateAndClassify_SurfacedToAdmin()
    {
        HttpClient client = _factory.CreateClient();

        // Unique but real-route-shaped path (/product/:id) so it is recordable and still
        // independent of other tests sharing the in-memory DB.
        string path = "/product/agg-" + Guid.NewGuid().ToString("N");

        // Two human renders + one crawler render of the same page.
        Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(Beacon(path, HumanUa, Secret))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(Beacon(path, HumanUa, Secret))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(Beacon(path, "Googlebot/2.1", Secret))).StatusCode);

        // Surface is admin-only.
        await RegisterAdmin(client, _factory, "analytics-admin@test.com");
        PageViewStatsResponse? stats = await client.GetFromJsonAsync<PageViewStatsResponse>(
            "/api/seo/traffic/page-views?days=2&limit=200", JsonOptions);

        Assert.NotNull(stats);
        PageViewTopPage? page = stats!.TopPages.FirstOrDefault(p => p.Path == path);
        Assert.NotNull(page);
        Assert.Equal(2, page!.Humans);
        Assert.Equal(1, page.Bots);
    }

    [Fact]
    public async Task PageView_UnknownPath_IsAccepted_ButNotRecorded()
    {
        HttpClient client = _factory.CreateClient();

        // A path that matches no real route (what the SPA serves from its "**" not-found
        // route with a 200). The beacon is accepted (204) but must not create a row, so it
        // can't be used to flood PageViewDaily with attacker-chosen paths.
        string junk = "/not-a-real-route-" + Guid.NewGuid().ToString("N");
        Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(Beacon(junk, HumanUa, Secret))).StatusCode);

        await RegisterAdmin(client, _factory, "analytics-junk-admin@test.com");
        PageViewStatsResponse? stats = await client.GetFromJsonAsync<PageViewStatsResponse>(
            "/api/seo/traffic/page-views?days=2&limit=500", JsonOptions);

        Assert.NotNull(stats);
        Assert.DoesNotContain(stats!.TopPages, p => p.Path == junk);
    }

    [Fact]
    public async Task PageViews_Surface_RequiresAdmin()
    {
        HttpClient client = _factory.CreateClient();
        HttpResponseMessage anon = await client.GetAsync("/api/seo/traffic/page-views");
        Assert.Equal(HttpStatusCode.Unauthorized, anon.StatusCode);

        await RegisterAndLogin(client, "analytics-customer@test.com");
        HttpResponseMessage customer = await client.GetAsync("/api/seo/traffic/page-views");
        Assert.Equal(HttpStatusCode.Forbidden, customer.StatusCode);
    }

    private record PageViewStatsResponse(
        int Days, long HumanViews, long BotViews, DateOnly? LastDataDate,
        List<PageViewDailyPoint> Daily, List<PageViewTopPage> TopPages, List<PageViewTopCountry> TopCountries);
    private record PageViewDailyPoint(DateOnly Date, long Humans, long Bots);
    private record PageViewTopPage(string Path, long Humans, long Bots);
    private record PageViewTopCountry(string Country, long Humans);
}
