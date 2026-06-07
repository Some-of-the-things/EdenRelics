using System.Net;
using System.Net.Http.Json;
using static Eden_Relics_BE.Tests.Helpers;

namespace Eden_Relics_BE.Tests;

public class BlogPreviewTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public BlogPreviewTests(ApiFactory factory)
    {
        _factory = factory;
    }

    private static async Task<string> CreateDraft(HttpClient adminClient, string title)
    {
        HttpResponseMessage resp = await adminClient.PostAsJsonAsync("/api/blog", new
        {
            title,
            content = "<p>Unpublished draft body.</p>",
            excerpt = (string?)null,
            featuredImageUrl = (string?)null,
            author = "Tester",
            published = false,
        });
        resp.EnsureSuccessStatusCode();
        BlogPostSlug? created = await resp.Content.ReadFromJsonAsync<BlogPostSlug>(JsonOptions);
        return created!.Slug;
    }

    [Fact]
    public async Task Preview_AsAdmin_ReturnsUnpublishedDraft_WhilePublicStill404s()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "blog-preview-admin@test.com");
        string slug = await CreateDraft(client, "Preview Admin Draft");

        // Admin preview sees the draft.
        HttpResponseMessage preview = await client.GetAsync($"/api/blog/preview/{slug}");
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);

        // Public endpoint still hides it.
        HttpResponseMessage pub = await client.GetAsync($"/api/blog/{slug}");
        Assert.Equal(HttpStatusCode.NotFound, pub.StatusCode);
    }

    [Fact]
    public async Task Preview_Anonymous_Returns401()
    {
        HttpClient admin = _factory.CreateClient();
        await RegisterAdmin(admin, _factory, "blog-preview-admin2@test.com");
        string slug = await CreateDraft(admin, "Preview Anon Draft");

        HttpClient anon = _factory.CreateClient();
        HttpResponseMessage preview = await anon.GetAsync($"/api/blog/preview/{slug}");
        Assert.Equal(HttpStatusCode.Unauthorized, preview.StatusCode);
    }

    [Fact]
    public async Task Preview_NonAdmin_Returns403()
    {
        HttpClient admin = _factory.CreateClient();
        await RegisterAdmin(admin, _factory, "blog-preview-admin3@test.com");
        string slug = await CreateDraft(admin, "Preview NonAdmin Draft");

        HttpClient user = _factory.CreateClient();
        await RegisterAndLogin(user, "blog-preview-user@test.com");
        HttpResponseMessage preview = await user.GetAsync($"/api/blog/preview/{slug}");
        Assert.Equal(HttpStatusCode.Forbidden, preview.StatusCode);
    }

    private record BlogPostSlug(string Slug);
}
