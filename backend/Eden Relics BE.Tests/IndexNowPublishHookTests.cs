using Eden_Relics_BE.Data.Entities;
using Eden_Relics_BE.Repositories;
using Eden_Relics_BE.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Eden_Relics_BE.Tests;

/// <summary>
/// The hooks are the point of IndexNow — a one-shot submission goes stale the moment the next
/// thing publishes. These pin down that publishing pings, that not publishing doesn't, and above
/// all that a broken search engine cannot take a publish down with it.
/// </summary>
public class IndexNowPublishHookTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public IndexNowPublishHookTests(ApiFactory factory)
    {
        _factory = factory;
    }

    /// <summary>Records what was submitted, so a hook firing is observable.</summary>
    private sealed class SpyIndexNow : IIndexNowService
    {
        public List<string> Submitted { get; } = [];

        public bool IsConfigured => true;
        public string? KeyLocation => "https://edenrelics.co.uk/key.txt";

        public Task<IndexNowResult> SubmitAsync(IReadOnlyCollection<string> urls, CancellationToken ct = default)
        {
            Submitted.AddRange(urls);
            return Task.FromResult(new IndexNowResult(true, urls.Count, 1, "ok", [200]));
        }

        public Task<IndexNowResult> SubmitAllAsync(CancellationToken ct = default) => SubmitAsync([], ct);

        public Task<IndexNowResult> SubmitPathsAsync(IReadOnlyCollection<string> paths, CancellationToken ct = default) =>
            SubmitAsync(paths.Select(p => $"https://edenrelics.co.uk{p}").ToList(), ct);
    }

    private sealed class NoSitemap : ISitemapService
    {
        public Task<string> BuildSitemapXmlAsync() => Task.FromResult(string.Empty);
        public Task<IReadOnlyList<string>> GetIndexableUrlsAsync() =>
            Task.FromResult<IReadOnlyList<string>>([]);
    }

    private static (IBlogService Blog, SpyIndexNow Spy) BuildBlog(IServiceScope scope)
    {
        SpyIndexNow spy = new();
        IRepository<BlogPost> repo = scope.ServiceProvider.GetRequiredService<IRepository<BlogPost>>();
        return (new BlogService(repo, spy), spy);
    }

    private static CreateBlogPostDto NewPost(string title, bool published) =>
        new(title, "body", null, null, null, published);

    [Fact]
    public async Task PublishingABlogPostSubmitsItsUrl()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        (IBlogService blog, SpyIndexNow spy) = BuildBlog(scope);

        BlogPostDto created = await blog.CreateAsync(NewPost("Dating a cut-label wool dress", published: true));

        Assert.Single(spy.Submitted);
        Assert.Equal($"https://edenrelics.co.uk/blog/{created.Slug}", spy.Submitted[0]);
    }

    [Fact]
    public async Task SavingADraftSubmitsNothing()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        (IBlogService blog, SpyIndexNow spy) = BuildBlog(scope);

        await blog.CreateAsync(NewPost("Half-written draft", published: false));

        Assert.Empty(spy.Submitted);
    }

    /// <summary>
    /// A rewritten post is a changed URL — the whole point of the protocol — so editing a live
    /// post pings, not only the transition from draft to published.
    /// </summary>
    [Fact]
    public async Task EditingAnAlreadyLivePostSubmitsAgain()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        (IBlogService blog, SpyIndexNow spy) = BuildBlog(scope);

        BlogPostDto created = await blog.CreateAsync(NewPost("Identifying St Michael labels", published: true));
        spy.Submitted.Clear();

        await blog.UpdateAsync(created.Id, new UpdateBlogPostDto(null, "revised body", null, null, null, null));

        Assert.Single(spy.Submitted);
        Assert.Contains(created.Slug, spy.Submitted[0]);
    }

    /// <summary>
    /// The one that matters, and it tests the REAL service rather than a spy: publish paths await
    /// the ping inline, so IIndexNowService's "must not throw" contract is what stands between a
    /// search engine being unreachable and an editor's save failing. Points a switched-on service
    /// at an unroutable endpoint — DNS failure, the least forgiving case.
    /// </summary>
    [Fact]
    public async Task AnUnreachableSearchEngineDoesNotThrow()
    {
        IHttpClientFactory factory = _factory.Services.GetRequiredService<IHttpClientFactory>();
        IndexNowService service = new(
            factory,
            new NoSitemap(),
            Options.Create(new IndexNowOptions
            {
                Enabled = true,
                Key = "testkey",
                Host = "edenrelics.co.uk",
                Endpoint = "https://indexnow.invalid-host-that-cannot-resolve.test/indexnow",
            }),
            NullLogger<IndexNowService>.Instance);

        IndexNowResult result = await service.SubmitPathsAsync(["/blog/anything"]);

        Assert.False(result.Submitted);
        Assert.Equal([0], result.StatusCodes);
    }

    /// <summary>And with that contract holding, a publish survives it end to end.</summary>
    [Fact]
    public async Task APublishSurvivesAnUnreachableSearchEngine()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        IHttpClientFactory httpFactory = _factory.Services.GetRequiredService<IHttpClientFactory>();
        IRepository<BlogPost> repo = scope.ServiceProvider.GetRequiredService<IRepository<BlogPost>>();
        IndexNowService broken = new(
            httpFactory,
            new NoSitemap(),
            Options.Create(new IndexNowOptions
            {
                Enabled = true,
                Key = "testkey",
                Host = "edenrelics.co.uk",
                Endpoint = "https://indexnow.invalid-host-that-cannot-resolve.test/indexnow",
            }),
            NullLogger<IndexNowService>.Instance);
        BlogService blog = new(repo, broken);

        BlogPostDto created = await blog.CreateAsync(NewPost("Publish must survive a dead engine", published: true));

        Assert.NotNull(await blog.GetByIdForAdminAsync(created.Id));
    }
}
