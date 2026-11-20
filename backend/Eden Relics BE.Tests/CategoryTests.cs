using System.Net;
using System.Net.Http.Json;
using Eden_Relics_BE.Data;
using Eden_Relics_BE.DTOs;
using Eden_Relics_BE.Services;
using Microsoft.Extensions.DependencyInjection;
using static Eden_Relics_BE.Tests.Helpers;

namespace Eden_Relics_BE.Tests;

/// <summary>
/// Admin-managed sub-categories under /designers, /style and /dresses: the migrated launch pages,
/// create/edit/delete with redirects so old URLs keep working, and keyword-plus-ticks membership.
/// </summary>
public class CategoryTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    /// <summary>Seeded test product "Bohemian Maxi Dress".</summary>
    private static readonly Guid MaxiDressId = Guid.Parse("a1b2c3d4-0001-0000-0000-000000000001");

    public CategoryTests(ApiFactory factory)
    {
        _factory = factory;
    }

    private async Task<HttpClient> AdminClient(string email)
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, email);
        return client;
    }

    private static object NewSub(string hub, string name, string? slug = null, bool visible = true, string[]? include = null) => new
    {
        hubKey = hub,
        name,
        slug,
        tagline = "A short line",
        intro = "A short description.",
        body = "",
        metaTitle = $"Vintage {name}",
        metaDescription = $"Vintage {name} pieces.",
        isVisible = visible,
        includeKeywords = include ?? new[] { name.ToLowerInvariant() },
        excludeKeywords = Array.Empty<string>(),
    };

    private async Task<CategoriesDto> GetPublic()
    {
        return (await _factory.CreateClient().GetFromJsonAsync<CategoriesDto>("/api/categories", JsonOptions))!;
    }

    private static async Task<SubCategoryDto> Create(HttpClient admin, object body)
    {
        HttpResponseMessage response = await admin.PostAsJsonAsync("/api/categories/admin/sub-categories", body);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<SubCategoryDto>(JsonOptions))!;
    }

    // ---- Migrated launch pages ----

    [Fact]
    public async Task Public_HasTheFifteenLaunchPagesWithTheirUrls()
    {
        CategoriesDto dto = await GetPublic();

        Assert.Equal(["designers", "style", "dresses"], dto.Hubs.Select(h => h.Key));
        Assert.Equal(10, dto.Hubs.Single(h => h.Key == "designers").SubCategories.Count);
        Assert.Equal(
            ["cottagecore", "prairie", "boho"],
            dto.Hubs.Single(h => h.Key == "style").SubCategories.Select(s => s.Slug).Take(3));
        Assert.Equal(["maxi", "midi"], dto.Hubs.Single(h => h.Key == "dresses").SubCategories.Select(s => s.Slug));

        SubCategoryDto laura = dto.Hubs.Single(h => h.Key == "designers").SubCategories.Single(s => s.Slug == "laura-ashley");
        Assert.Equal("Laura Ashley", laura.Name);
        Assert.Equal("laura-ashley", laura.ContentKey);
        Assert.Contains("laura ashley", laura.IncludeKeywords);
    }

    [Fact]
    public async Task Seed_IsIdempotent()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        EdenRelicsDbContext db = scope.ServiceProvider.GetRequiredService<EdenRelicsDbContext>();
        int before = db.SubCategories.Count();
        await CategorySeed.EnsureSeedDataAsync(db);
        Assert.Equal(before, db.SubCategories.Count());
        Assert.Equal(3, db.CategoryHubs.Count());
    }

    // ---- Create / edit / delete ----

    [Fact]
    public async Task Create_Whimsigoth_ShowsOnPublicAndInSitemap()
    {
        HttpClient admin = await AdminClient("cat-create@test.com");
        SubCategoryDto created = await Create(admin, NewSub("style", "Whimsigoth"));

        Assert.Equal("whimsigoth", created.Slug);
        CategoriesDto dto = await GetPublic();
        Assert.Contains(dto.Hubs.Single(h => h.Key == "style").SubCategories, s => s.Slug == "whimsigoth");

        string sitemap = await _factory.CreateClient().GetStringAsync("/api/sitemap.xml");
        Assert.Contains("https://edenrelics.co.uk/style/whimsigoth</loc>", sitemap);
    }

    [Fact]
    public async Task Create_DuplicateSlugInSameHub_Returns400()
    {
        HttpClient admin = await AdminClient("cat-dupe@test.com");
        HttpResponseMessage response = await admin.PostAsJsonAsync(
            "/api/categories/admin/sub-categories", NewSub("style", "Cottagecore Again", slug: "cottagecore"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_BadSlug_Returns400()
    {
        HttpClient admin = await AdminClient("cat-badslug@test.com");
        HttpResponseMessage response = await admin.PostAsJsonAsync(
            "/api/categories/admin/sub-categories", NewSub("style", "Bad", slug: "Bad Slug!"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_UnknownHub_Returns400()
    {
        HttpClient admin = await AdminClient("cat-badhub@test.com");
        HttpResponseMessage response = await admin.PostAsJsonAsync(
            "/api/categories/admin/sub-categories", NewSub("decades", "Seventies"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AdminEndpoints_RequireAdmin()
    {
        HttpClient anon = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/categories/admin")).StatusCode);

        HttpClient user = _factory.CreateClient();
        await RegisterAndLogin(user, "cat-notadmin@test.com");
        HttpResponseMessage response = await user.PostAsJsonAsync("/api/categories/admin/sub-categories", NewSub("style", "Nope"));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task SlugChange_AddsRedirect_AndRepointsEarlierOnes()
    {
        HttpClient admin = await AdminClient("cat-rename@test.com");
        SubCategoryDto sub = await Create(admin, NewSub("style", "Rename Test", slug: "rename-a"));

        await admin.PutAsJsonAsync($"/api/categories/admin/sub-categories/{sub.Id}", NewSub("style", "Rename Test", slug: "rename-b"));
        await admin.PutAsJsonAsync($"/api/categories/admin/sub-categories/{sub.Id}", NewSub("style", "Rename Test", slug: "rename-c"));

        CategoriesDto dto = await GetPublic();
        Assert.Contains(dto.Redirects, r => r.HubKey == "style" && r.FromSlug == "rename-a" && r.ToPath == "/style/rename-c");
        Assert.Contains(dto.Redirects, r => r.HubKey == "style" && r.FromSlug == "rename-b" && r.ToPath == "/style/rename-c");
    }

    [Fact]
    public async Task NewPageOnARedirectedSlug_TakesTheUrlBack()
    {
        HttpClient admin = await AdminClient("cat-reclaim@test.com");
        SubCategoryDto sub = await Create(admin, NewSub("style", "Reclaim", slug: "reclaim-old"));
        await admin.PutAsJsonAsync($"/api/categories/admin/sub-categories/{sub.Id}", NewSub("style", "Reclaim", slug: "reclaim-new"));

        await Create(admin, NewSub("style", "Reclaim Two", slug: "reclaim-old"));

        CategoriesDto dto = await GetPublic();
        Assert.DoesNotContain(dto.Redirects, r => r.FromSlug == "reclaim-old");
    }

    [Fact]
    public async Task Delete_RedirectsToTheHub_AndDropsFromPublic()
    {
        HttpClient admin = await AdminClient("cat-delete@test.com");
        SubCategoryDto sub = await Create(admin, NewSub("dresses", "Tea Dresses", slug: "tea"));

        HttpResponseMessage response = await admin.DeleteAsync($"/api/categories/admin/sub-categories/{sub.Id}");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        CategoriesDto dto = await GetPublic();
        Assert.DoesNotContain(dto.Hubs.Single(h => h.Key == "dresses").SubCategories, s => s.Slug == "tea");
        Assert.Contains(dto.Redirects, r => r.HubKey == "dresses" && r.FromSlug == "tea" && r.ToPath == "/dresses");
    }

    [Fact]
    public async Task Hidden_IsLeftOutOfPublicAndSitemap_ButListedAsHidden()
    {
        HttpClient admin = await AdminClient("cat-hidden@test.com");
        await Create(admin, NewSub("style", "Secret Style", slug: "secret-style", visible: false));

        CategoriesDto dto = await GetPublic();
        Assert.DoesNotContain(dto.Hubs.SelectMany(h => h.SubCategories), s => s.Slug == "secret-style");
        Assert.Contains(dto.Hidden, h => h.HubKey == "style" && h.Slug == "secret-style");

        CategoriesDto adminDto = (await admin.GetFromJsonAsync<CategoriesDto>("/api/categories/admin", JsonOptions))!;
        Assert.Contains(adminDto.Hubs.SelectMany(h => h.SubCategories), s => s.Slug == "secret-style");

        string sitemap = await _factory.CreateClient().GetStringAsync("/api/sitemap.xml");
        Assert.DoesNotContain("/style/secret-style<", sitemap);
    }

    [Fact]
    public async Task Reorder_ChangesDisplayOrder()
    {
        HttpClient admin = await AdminClient("cat-reorder@test.com");
        CategoriesDto before = (await admin.GetFromJsonAsync<CategoriesDto>("/api/categories/admin", JsonOptions))!;
        List<Guid> ids = before.Hubs.Single(h => h.Key == "dresses").SubCategories.Select(s => s.Id).Reverse().ToList();

        HttpResponseMessage response = await admin.PutAsJsonAsync("/api/categories/admin/hubs/dresses/order", new { ids });
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        CategoriesDto after = (await admin.GetFromJsonAsync<CategoriesDto>("/api/categories/admin", JsonOptions))!;
        Assert.Equal(ids, after.Hubs.Single(h => h.Key == "dresses").SubCategories.Select(s => s.Id));

        // The fixture's database is shared across this class, so put the launch order back.
        List<Guid> original = before.Hubs.Single(h => h.Key == "dresses").SubCategories.Select(s => s.Id).ToList();
        await admin.PutAsJsonAsync("/api/categories/admin/hubs/dresses/order", new { ids = original });
    }

    // ---- Membership: keywords plus ticks ----

    [Fact]
    public async Task BulkAdd_NonMatchingPiece_StoresAnAddition_AndRemovingClearsIt()
    {
        HttpClient admin = await AdminClient("cat-bulk@test.com");
        SubCategoryDto sub = await Create(admin, NewSub("style", "Bulk Style", slug: "bulk-style", include: ["zzz-nomatch"]));

        HttpResponseMessage added = await admin.PutAsJsonAsync(
            $"/api/categories/admin/sub-categories/{sub.Id}/products", new { add = new[] { MaxiDressId } });
        SubCategoryDto afterAdd = (await added.Content.ReadFromJsonAsync<SubCategoryDto>(JsonOptions))!;
        Assert.Equal([MaxiDressId], afterAdd.AddedProductIds);

        HttpResponseMessage removed = await admin.PutAsJsonAsync(
            $"/api/categories/admin/sub-categories/{sub.Id}/products", new { remove = new[] { MaxiDressId } });
        SubCategoryDto afterRemove = (await removed.Content.ReadFromJsonAsync<SubCategoryDto>(JsonOptions))!;
        Assert.Empty(afterRemove.AddedProductIds);
        Assert.Empty(afterRemove.RemovedProductIds);
    }

    [Fact]
    public async Task RemovingAKeywordMatch_StoresARemoval_AndReAddingClearsIt()
    {
        HttpClient admin = await AdminClient("cat-kwremove@test.com");
        SubCategoryDto sub = await Create(admin, NewSub("dresses", "Keyword Maxi", slug: "keyword-maxi", include: ["maxi"]));

        HttpResponseMessage removed = await admin.PutAsJsonAsync(
            $"/api/categories/admin/sub-categories/{sub.Id}/products", new { remove = new[] { MaxiDressId } });
        Assert.Equal([MaxiDressId], (await removed.Content.ReadFromJsonAsync<SubCategoryDto>(JsonOptions))!.RemovedProductIds);

        HttpResponseMessage readded = await admin.PutAsJsonAsync(
            $"/api/categories/admin/sub-categories/{sub.Id}/products", new { add = new[] { MaxiDressId } });
        SubCategoryDto final = (await readded.Content.ReadFromJsonAsync<SubCategoryDto>(JsonOptions))!;
        Assert.Empty(final.RemovedProductIds);
        Assert.Empty(final.AddedProductIds);
    }

    [Fact]
    public async Task ProductTicks_StoreOnlyTheDifferencesFromKeywords()
    {
        HttpClient admin = await AdminClient("cat-ticks@test.com");
        SubCategoryDto ticked = await Create(admin, NewSub("style", "Ticked Style", slug: "ticked-style", include: ["zzz-nomatch"]));
        SubCategoryDto matching = await Create(admin, NewSub("dresses", "Ticks Maxi", slug: "ticks-maxi", include: ["maxi"]));

        // Tick the non-matching one and leave the keyword match ticked: one addition, nothing else.
        HttpResponseMessage response = await admin.PutAsJsonAsync(
            $"/api/categories/admin/products/{MaxiDressId}", new { subCategoryIds = new[] { ticked.Id, matching.Id } });
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        CategoriesDto dto = (await admin.GetFromJsonAsync<CategoriesDto>("/api/categories/admin", JsonOptions))!;
        List<SubCategoryDto> all = dto.Hubs.SelectMany(h => h.SubCategories).ToList();
        Assert.Contains(MaxiDressId, all.Single(s => s.Id == ticked.Id).AddedProductIds);
        Assert.Empty(all.Single(s => s.Id == matching.Id).AddedProductIds);
        Assert.Empty(all.Single(s => s.Id == matching.Id).RemovedProductIds);
    }

    [Fact]
    public async Task ProductTicks_UnknownProduct_Returns404()
    {
        HttpClient admin = await AdminClient("cat-ticks404@test.com");
        HttpResponseMessage response = await admin.PutAsJsonAsync(
            $"/api/categories/admin/products/{Guid.NewGuid()}", new { subCategoryIds = Array.Empty<Guid>() });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("1970s Boho Maxi Dress", new[] { "maxi" }, new string[0], true)]
    [InlineData("1970s Boho Maxi Dress", new[] { "MAXI" }, new string[0], true)]
    [InlineData("1970s Boho Maxi Skirt", new[] { "maxi" }, new[] { "skirt" }, false)]
    [InlineData("1990s Midi Dress", new[] { "maxi" }, new string[0], false)]
    [InlineData("Anything", new string[0], new string[0], false)]
    public void KeywordMatches_FollowsTheFrontendRule(string name, string[] include, string[] exclude, bool expected)
    {
        Assert.Equal(expected, CategoryService.KeywordMatches(name, include, exclude));
    }
}
