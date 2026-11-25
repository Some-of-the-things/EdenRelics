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
        List<Guid> ProductIds,
        bool CreatePage = false);

    private record Page(string Path, string Eyebrow, string Title, string Description, List<Guid> ProductIds);

    private record Created(Guid Id);

    private static Section Custom(string title, string link, bool createPage, params Guid[] ids) =>
        new("custom", "Just in", title, "Cosy pieces.", "See them all", link, true, ids.ToList(), createPage);

    private static async Task<Guid> ListLivePiece(HttpClient admin, string name)
    {
        HttpResponseMessage r = await admin.PostAsJsonAsync("/api/products", new
        {
            name,
            description = "A lovely piece.",
            price = 60m,
            costPrice = 10m,
            era = "1970s",
            category = "1970s",
            size = "10",
            condition = "Excellent",
            imageUrl = "https://img.example/x.webp",
            inStock = true,
        });
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        return (await r.Content.ReadFromJsonAsync<Created>(JsonOptions))!.Id;
    }

    private static Section TopPicks(bool visible = true) =>
        new("top-picks", "Our Top Picks", "Our Top Picks", "Hand-chosen.", "See all top picks", "/top-picks", visible, []);

    private static Section Latest(bool visible = true) =>
        new("latest-collection", "Our Latest Collection", "The Wildflower Edit", "Florals.", "Browse", "/collections/wildflower-edit", visible, []);

    private static async Task<HttpResponseMessage> Save(HttpClient client, params Section[] sections) =>
        await client.PutAsJsonAsync("/api/home-sections/admin", new { sections });

    [Fact]
    public async Task Public_BeforeAnySave_ReturnsTheBuiltInSections_LatestCollectionFirst()
    {
        using ApiFactory factory = new();
        HttpClient client = factory.CreateClient();

        List<Section>? sections = await client.GetFromJsonAsync<List<Section>>("/api/home-sections", JsonOptions);

        Assert.NotNull(sections);
        Assert.Equal(["latest-collection", "top-picks"], sections.Select(s => s.Kind));
        Assert.Equal("Noir", sections[0].Title);
        Assert.Equal("/collections/noir", sections[0].ButtonLink);
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

    [Fact]
    public async Task CreatePage_AtANewAddress_ServesThePageAndListsItInTheSitemap()
    {
        using ApiFactory factory = new();
        HttpClient admin = factory.CreateClient();
        await RegisterAdmin(admin, factory, "home-sections-page@test.com");
        Guid piece = await ListLivePiece(admin, "Autumn Cardigan");

        HttpResponseMessage response = await Save(admin, Latest(), TopPicks(), Custom("Autumn Knits", "/Autumn-Knits/", true, piece));
        response.EnsureSuccessStatusCode();
        List<Section> saved = (await response.Content.ReadFromJsonAsync<List<Section>>(JsonOptions))!;
        // The address is tidied to the form the page is served at.
        Assert.True(saved[2].CreatePage);
        Assert.Equal("/autumn-knits", saved[2].ButtonLink);

        HttpClient anon = factory.CreateClient();
        Page? page = await anon.GetFromJsonAsync<Page>("/api/home-sections/page?path=/autumn-knits", JsonOptions);
        Assert.NotNull(page);
        Assert.Equal("Autumn Knits", page.Title);
        Assert.Equal([piece], page.ProductIds);

        string sitemap = await anon.GetStringAsync("/api/sitemap.xml");
        Assert.Contains("<loc>https://edenrelics.co.uk/autumn-knits</loc>", sitemap);
    }

    [Fact]
    public async Task CreatePage_AtAnExistingSiteAddress_JustLinks_AndCreatesNothing()
    {
        using ApiFactory factory = new();
        HttpClient admin = factory.CreateClient();
        await RegisterAdmin(admin, factory, "home-sections-existing@test.com");

        HttpResponseMessage response = await Save(admin, Latest(), TopPicks(), Custom("On Sale", "/shop/sale", true));
        response.EnsureSuccessStatusCode();
        List<Section> saved = (await response.Content.ReadFromJsonAsync<List<Section>>(JsonOptions))!;
        Assert.False(saved[2].CreatePage);
        Assert.Equal("/shop/sale", saved[2].ButtonLink);

        HttpClient anon = factory.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await anon.GetAsync("/api/home-sections/page?path=/shop/sale")).StatusCode);
    }

    [Fact]
    public async Task Page_IsGone_WhenTheTickIsTakenOff_AndUnknownAddressesAre404()
    {
        using ApiFactory factory = new();
        HttpClient admin = factory.CreateClient();
        await RegisterAdmin(admin, factory, "home-sections-untick@test.com");
        (await Save(admin, Latest(), TopPicks(), Custom("Autumn Knits", "/autumn-knits", true))).EnsureSuccessStatusCode();
        (await Save(admin, Latest(), TopPicks(), Custom("Autumn Knits", "/autumn-knits", false))).EnsureSuccessStatusCode();

        HttpClient anon = factory.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await anon.GetAsync("/api/home-sections/page?path=/autumn-knits")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await anon.GetAsync("/api/home-sections/page?path=/never-made")).StatusCode);
    }

    [Theory]
    [InlineData("", "needs a")]
    [InlineData("/autumn_knits", "lowercase letters")]
    [InlineData("/a/b/c/d", "lowercase letters")]
    public async Task CreatePage_RejectsAMissingOrUnusableAddress(string link, string expected)
    {
        using ApiFactory factory = new();
        HttpClient admin = factory.CreateClient();
        await RegisterAdmin(admin, factory, $"home-sections-bad{link.Length}@test.com");
        Section custom = Custom("Autumn Knits", link, true) with { ButtonText = link.Length == 0 ? "" : "See them all" };

        HttpResponseMessage response = await Save(admin, Latest(), TopPicks(), custom);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(expected, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task CreatePage_RefusesTwoSectionsWantingTheSameAddress()
    {
        using ApiFactory factory = new();
        HttpClient admin = factory.CreateClient();
        await RegisterAdmin(admin, factory, "home-sections-dupe@test.com");

        HttpResponseMessage response = await Save(admin, Latest(), TopPicks(),
            Custom("Autumn Knits", "/autumn-knits", true), Custom("More Knits", "/autumn-knits", true));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Two sections", await response.Content.ReadAsStringAsync());
    }
}
