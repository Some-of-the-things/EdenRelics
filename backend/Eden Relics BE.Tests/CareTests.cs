using System.Net;
using System.Net.Http.Json;
using static Eden_Relics_BE.Tests.Helpers;

namespace Eden_Relics_BE.Tests;

public class CareTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public CareTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task PublicFabric_Unpublished_Returns404()
    {
        HttpClient client = _factory.CreateClient();
        // Viyella is seeded as an unreviewed draft — must not be served publicly.
        HttpResponseMessage resp = await client.GetAsync("/api/care/fabric/viyella");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Worklist_RequiresAdmin()
    {
        HttpClient client = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/care/admin/worklist")).StatusCode);

        await RegisterAndLogin(client, "care-customer@test.com");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/care/admin/worklist")).StatusCode);
    }

    [Fact]
    public async Task Worklist_ListsSeededViyella_AsOutstandingWithHeadTerms()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "care-worklist@test.com");

        List<WorklistItem>? items = await client.GetFromJsonAsync<List<WorklistItem>>(
            "/api/care/admin/worklist", JsonOptions);

        Assert.NotNull(items);
        WorklistItem? viyella = items!.FirstOrDefault(i => i.Slug == "viyella");
        Assert.NotNull(viyella);
        Assert.True(viyella!.NeedsAction);              // not yet published
        Assert.False(viyella.IsPublished);
        Assert.Contains("viyella fabric", viyella.TargetKeywords);   // head terms surfaced
        Assert.NotEmpty(viyella.ReviewNotes);
    }

    [Fact]
    public async Task CreateThenPublish_MakesFabricPubliclyVisible()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "care-publish@test.com");

        // Create a draft fabric (unique name so the test is independent of others).
        var create = new
        {
            name = "Test Crepe " + Guid.NewGuid().ToString("N")[..8],
            targetKeywords = new[] { "test crepe care" },
            washing = "Hand wash cold.",
        };
        HttpResponseMessage createResp = await client.PostAsJsonAsync("/api/care/admin/fabric", create);
        Assert.Equal(HttpStatusCode.OK, createResp.StatusCode);
        FabricDto? created = await createResp.Content.ReadFromJsonAsync<FabricDto>(JsonOptions);
        Assert.NotNull(created);
        Assert.Equal("Draft", created!.Status);
        Assert.False(created.IsPublished);

        // Not visible publicly while a draft.
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/care/fabric/{created.Slug}")).StatusCode);

        // Approve + publish.
        HttpResponseMessage pubResp = await client.PostAsJsonAsync(
            $"/api/care/admin/fabric/{created.Id}/publish", new { published = true });
        Assert.Equal(HttpStatusCode.OK, pubResp.StatusCode);
        FabricDto? published = await pubResp.Content.ReadFromJsonAsync<FabricDto>(JsonOptions);
        Assert.Equal("ExpertApproved", published!.Status);
        Assert.True(published.IsPublished);
        Assert.NotNull(published.LastReviewedUtc);

        // Now served publicly.
        HttpResponseMessage publicResp = await client.GetAsync($"/api/care/fabric/{created.Slug}");
        Assert.Equal(HttpStatusCode.OK, publicResp.StatusCode);
        FabricDto? pub = await publicResp.Content.ReadFromJsonAsync<FabricDto>(JsonOptions);
        Assert.Equal(created.Slug, pub!.Slug);
        Assert.Equal("Hand wash cold.", pub.Washing);
    }

    [Fact]
    public async Task PublishUnknownFabric_Returns404()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "care-404@test.com");

        HttpResponseMessage resp = await client.PostAsJsonAsync(
            $"/api/care/admin/fabric/{Guid.NewGuid()}/publish", new { published = true });
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    private record WorklistItem(
        Guid Id, string Type, string Name, string Slug, string Status,
        bool IsPublished, bool NeedsAction, List<string> TargetKeywords,
        string ReviewNotes, DateTime? LastReviewedUtc, DateTime UpdatedAtUtc);

    private record FabricDto(
        Guid Id, string Slug, string Name, List<string> TargetKeywords,
        string Washing, string Status, bool IsPublished, DateTime? LastReviewedUtc);
}
