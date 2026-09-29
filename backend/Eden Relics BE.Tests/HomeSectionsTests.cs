using System.Net;
using System.Net.Http.Json;
using static Eden_Relics_BE.Tests.Helpers;

namespace Eden_Relics_BE.Tests;

/// <summary>
/// The homepage sections an admin arranges on the Home Sections tab. Each test builds its own
/// <see cref="ApiFactory"/>: the section list is global state replaced wholesale on save, so tests
/// sharing one in-memory database would overwrite each other.
/// </summary>
public class HomeSectionsTests
{
    private record Section(
        string Kind,
        string Eyebrow,
        string Title,
        string Description,
        string ButtonText,
        string ButtonLink,
        bool Visible,
        List<Guid> ProductIds);

    private static Section TopPicks(bool visible = true) =>
        new("top-picks", "Our Top Picks", "Our Top Picks", "Hand-chosen.", "See all top picks", "/top-picks", visible, []);

    private static Section Latest(bool visible = true) =>
        new("latest-collection", "Our Latest Collection", "The Wildflower Edit", "Florals.", "Browse", "/collections/wildflower-edit", visible, []);

    private static async Task<HttpResponseMessage> Save(HttpClient client, params Section[] sections) =>
        await client.PutAsJsonAsync("/api/home-sections/admin", new { sections });

    [Fact]
    public async Task Public_BeforeAnySave_ReturnsTheTwoOriginalSectionsInTheirOriginalOrder()
    {
        using ApiFactory factory = new();
        HttpClient client = factory.CreateClient();

        List<Section>? sections = await client.GetFromJsonAsync<List<Section>>("/api/home-sections", JsonOptions);

        Assert.NotNull(sections);
        Assert.Equal(["top-picks", "latest-collection"], sections.Select(s => s.Kind));
        Assert.Equal("The Wildflower Edit", sections[1].Title);
    }

    [Fact]
    public async Task Save_AsAdmin_ReordersRenamesAndAddsACustomSection()
    {
        using ApiFactory factory = new();
        HttpClient client = factory.CreateClient();
        await RegisterAdmin(client, factory, "home-sections-save@test.com");
        Guid pieceId = Guid.NewGuid();

        HttpResponseMessage response = await Save(client,
            Latest(),
            new Section("custom", "Just in", "Autumn Knits", "Cosy pieces.", "Shop the sale", "/shop/sale", true, [pieceId, pieceId]),
            TopPicks() with { Title = "Editor's Favourites" });
        response.EnsureSuccessStatusCode();

        List<Section>? sections = await client.GetFromJsonAsync<List<Section>>("/api/home-sections", JsonOptions);
        Assert.NotNull(sections);
        Assert.Equal(["latest-collection", "custom", "top-picks"], sections.Select(s => s.Kind));
        Assert.Equal("Editor's Favourites", sections[2].Title);
        // Duplicate picks collapse to one, keeping order.
        Assert.Equal([pieceId], sections[1].ProductIds);
    }

    [Fact]
    public async Task Public_LeavesOutHiddenSections_ButAlwaysIncludesTopPicks()
    {
        using ApiFactory factory = new();
        HttpClient client = factory.CreateClient();
        await RegisterAdmin(client, factory, "home-sections-hidden@test.com");

        HttpResponseMessage response = await Save(client,
            TopPicks(visible: false),
            Latest(visible: false),
            new Section("custom", "", "Hidden For Now", "", "", "", false, []));
        response.EnsureSuccessStatusCode();

        List<Section>? publicSections = await client.GetFromJsonAsync<List<Section>>("/api/home-sections", JsonOptions);
        List<Section>? adminSections = await client.GetFromJsonAsync<List<Section>>("/api/home-sections/admin", JsonOptions);

        // Top Picks follows its own switch, so hiding it here must not be the second off switch.
        Assert.Equal(["top-picks"], publicSections!.Select(s => s.Kind));
        Assert.Equal(3, adminSections!.Count);
    }

    [Fact]
    public async Task Save_WithoutABuiltInSection_IsRejectedAndChangesNothing()
    {
        using ApiFactory factory = new();
        HttpClient client = factory.CreateClient();
        await RegisterAdmin(client, factory, "home-sections-builtin@test.com");

        HttpResponseMessage response = await Save(client, TopPicks());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        List<Section>? sections = await client.GetFromJsonAsync<List<Section>>("/api/home-sections", JsonOptions);
        Assert.Equal(2, sections!.Count);
    }

    [Theory]
    [InlineData("https://example.com")]
    [InlineData("//example.com/shop")]
    [InlineData("shop/sale")]
    public async Task Save_WithAnOffSiteButtonLink_IsRejected(string link)
    {
        using ApiFactory factory = new();
        HttpClient client = factory.CreateClient();
        await RegisterAdmin(client, factory, "home-sections-link@test.com");

        HttpResponseMessage response = await Save(client, TopPicks(), Latest() with { ButtonLink = link });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Save_WithABlankTitle_IsRejected()
    {
        using ApiFactory factory = new();
        HttpClient client = factory.CreateClient();
        await RegisterAdmin(client, factory, "home-sections-title@test.com");

        HttpResponseMessage response = await Save(client, TopPicks(), Latest() with { Title = "   " });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Save_WithoutSigningIn_IsRefused()
    {
        using ApiFactory factory = new();
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await Save(client, TopPicks(), Latest());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Save_AsANonAdmin_IsForbidden()
    {
        using ApiFactory factory = new();
        HttpClient client = factory.CreateClient();
        await RegisterAndLogin(client, "home-sections-customer@test.com");

        HttpResponseMessage response = await Save(client, TopPicks(), Latest());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
