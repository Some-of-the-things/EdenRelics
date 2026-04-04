using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using SixLabors.ImageSharp;
using static Eden_Relics_BE.Tests.Helpers;

namespace Eden_Relics_BE.Tests;

public class BlogTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public BlogTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetAll_ReturnsOnlyPublishedPosts()
    {
        var client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "blog-getall@test.com");

        // Create published post
        await client.PostAsJsonAsync("/api/blog", new
        {
            title = "Published Post",
            content = "Content here",
            published = true
        });

        // Create draft post
        await client.PostAsJsonAsync("/api/blog", new
        {
            title = "Draft Post",
            content = "Draft content",
            published = false
        });

        // Unauthenticated client to test public endpoint
        var publicClient = _factory.CreateClient();
        var posts = await publicClient.GetFromJsonAsync<List<BlogPostSummaryResponse>>("/api/blog", JsonOptions);
        Assert.NotNull(posts);
        Assert.Contains(posts, p => p.Title == "Published Post");
        Assert.DoesNotContain(posts, p => p.Title == "Draft Post");
    }

    [Fact]
    public async Task GetAllAdmin_ReturnsAllPosts()
    {
        var client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "blog-getalladmin@test.com");

        await client.PostAsJsonAsync("/api/blog", new
        {
            title = "Admin Visible Published",
            content = "Content",
            published = true
        });
        await client.PostAsJsonAsync("/api/blog", new
        {
            title = "Admin Visible Draft",
            content = "Content",
            published = false
        });

        var posts = await client.GetFromJsonAsync<List<BlogPostSummaryResponse>>("/api/blog/admin/all", JsonOptions);
        Assert.NotNull(posts);
        Assert.Contains(posts, p => p.Title == "Admin Visible Published");
        Assert.Contains(posts, p => p.Title == "Admin Visible Draft");
    }

    [Fact]
    public async Task GetAllAdmin_AsCustomer_Returns403()
    {
        var client = _factory.CreateClient();
        await RegisterAndLogin(client, "blog-customer-admin@test.com");
        var response = await client.GetAsync("/api/blog/admin/all");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Create_AsAdmin_ReturnsCreatedPost()
    {
        var client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "blog-create@test.com");

        var response = await client.PostAsJsonAsync("/api/blog", new
        {
            title = "Test Blog Post",
            content = "<p>Hello World</p>",
            excerpt = "A test post",
            author = "Test Author",
            published = true
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var post = await response.Content.ReadFromJsonAsync<BlogPostResponse>(JsonOptions);
        Assert.NotNull(post);
        Assert.Equal("Test Blog Post", post.Title);
        Assert.Equal("test-blog-post", post.Slug);
        Assert.Equal("<p>Hello World</p>", post.Content);
        Assert.Equal("A test post", post.Excerpt);
        Assert.Equal("Test Author", post.Author);
        Assert.True(post.Published);
        Assert.NotNull(post.PublishedAtUtc);
    }

    [Fact]
    public async Task Create_Draft_HasNullPublishedAt()
    {
        var client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "blog-create-draft@test.com");

        var response = await client.PostAsJsonAsync("/api/blog", new
        {
            title = "Draft Only Post",
            content = "Draft content",
            published = false
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var post = await response.Content.ReadFromJsonAsync<BlogPostResponse>(JsonOptions);
        Assert.NotNull(post);
        Assert.False(post.Published);
        Assert.Null(post.PublishedAtUtc);
    }

    [Fact]
    public async Task Create_Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/blog", new
        {
            title = "Hack Post",
            content = "Content",
            published = true
        });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Create_AsCustomer_Returns403()
    {
        var client = _factory.CreateClient();
        await RegisterAndLogin(client, "blog-customer-create@test.com");

        var response = await client.PostAsJsonAsync("/api/blog", new
        {
            title = "Customer Post",
            content = "Content",
            published = true
        });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Create_DuplicateSlug_AppendsCounter()
    {
        var client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "blog-dup-slug@test.com");

        await client.PostAsJsonAsync("/api/blog", new
        {
            title = "Duplicate Title",
            content = "First",
            published = true
        });

        var response2 = await client.PostAsJsonAsync("/api/blog", new
        {
            title = "Duplicate Title",
            content = "Second",
            published = true
        });
        var post2 = await response2.Content.ReadFromJsonAsync<BlogPostResponse>(JsonOptions);
        Assert.NotNull(post2);
        Assert.Equal("duplicate-title-1", post2.Slug);
    }

    [Fact]
    public async Task GetBySlug_ReturnsPublishedPost()
    {
        var client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "blog-getbyslug@test.com");

        await client.PostAsJsonAsync("/api/blog", new
        {
            title = "Slug Test Post",
            content = "Content here",
            published = true
        });

        var publicClient = _factory.CreateClient();
        var post = await publicClient.GetFromJsonAsync<BlogPostResponse>("/api/blog/slug-test-post", JsonOptions);
        Assert.NotNull(post);
        Assert.Equal("Slug Test Post", post.Title);
    }

    [Fact]
    public async Task GetBySlug_UnpublishedPost_Returns404()
    {
        var client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "blog-slug-unpub@test.com");

        var createResponse = await client.PostAsJsonAsync("/api/blog", new
        {
            title = "Hidden Post For Slug",
            content = "Content",
            published = false
        });
        var created = await createResponse.Content.ReadFromJsonAsync<BlogPostResponse>(JsonOptions);

        var publicClient = _factory.CreateClient();
        var response = await publicClient.GetAsync($"/api/blog/{created!.Slug}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetBySlug_NonExistent_Returns404()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/blog/non-existent-slug");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetByIdAdmin_ReturnsPost()
    {
        var client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "blog-getbyid@test.com");

        var createResponse = await client.PostAsJsonAsync("/api/blog", new
        {
            title = "Get By Id Post",
            content = "Content",
            published = false
        });
        var created = await createResponse.Content.ReadFromJsonAsync<BlogPostResponse>(JsonOptions);

        var post = await client.GetFromJsonAsync<BlogPostResponse>($"/api/blog/admin/{created!.Id}", JsonOptions);
        Assert.NotNull(post);
        Assert.Equal("Get By Id Post", post.Title);
    }

    [Fact]
    public async Task GetByIdAdmin_NonExistent_Returns404()
    {
        var client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "blog-getbyid-404@test.com");
        var response = await client.GetAsync($"/api/blog/admin/{Guid.Empty}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Update_AsAdmin_ModifiesPost()
    {
        var client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "blog-update@test.com");

        var createResponse = await client.PostAsJsonAsync("/api/blog", new
        {
            title = "Original Title",
            content = "Original content",
            published = false
        });
        var created = await createResponse.Content.ReadFromJsonAsync<BlogPostResponse>(JsonOptions);

        var updateResponse = await client.PutAsJsonAsync($"/api/blog/{created!.Id}", new
        {
            title = "Updated Title",
            content = "Updated content"
        });
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var updated = await updateResponse.Content.ReadFromJsonAsync<BlogPostResponse>(JsonOptions);
        Assert.Equal("Updated Title", updated!.Title);
        Assert.Equal("Updated content", updated.Content);
    }

    [Fact]
    public async Task Update_PublishDraft_SetsPublishedAtUtc()
    {
        var client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "blog-publish@test.com");

        var createResponse = await client.PostAsJsonAsync("/api/blog", new
        {
            title = "Will Publish",
            content = "Content",
            published = false
        });
        var created = await createResponse.Content.ReadFromJsonAsync<BlogPostResponse>(JsonOptions);
        Assert.Null(created!.PublishedAtUtc);

        var updateResponse = await client.PutAsJsonAsync($"/api/blog/{created.Id}", new
        {
            published = true
        });
        var updated = await updateResponse.Content.ReadFromJsonAsync<BlogPostResponse>(JsonOptions);
        Assert.True(updated!.Published);
        Assert.NotNull(updated.PublishedAtUtc);
    }

    [Fact]
    public async Task Update_NonExistent_Returns404()
    {
        var client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "blog-update-404@test.com");

        var response = await client.PutAsJsonAsync($"/api/blog/{Guid.Empty}", new
        {
            title = "Nope"
        });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_AsAdmin_RemovesPost()
    {
        var client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "blog-delete@test.com");

        var createResponse = await client.PostAsJsonAsync("/api/blog", new
        {
            title = "To Delete Blog",
            content = "Content",
            published = true
        });
        var created = await createResponse.Content.ReadFromJsonAsync<BlogPostResponse>(JsonOptions);

        var deleteResponse = await client.DeleteAsync($"/api/blog/{created!.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var getResponse = await client.GetAsync($"/api/blog/admin/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task Delete_NonExistent_Returns404()
    {
        var client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "blog-delete-404@test.com");
        var response = await client.DeleteAsync($"/api/blog/{Guid.Empty}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UploadImage_AsAdmin_ReturnsImageUrl()
    {
        var client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "blog-upload@test.com");

        using var image = new SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>(100, 100);
        using var ms = new MemoryStream();
        image.SaveAsPng(ms);

        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(ms.ToArray());
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(fileContent, "file", "blog.png");

        var response = await client.PostAsync("/api/blog/upload-image", content);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<ImageUrlResponse>(JsonOptions);
        Assert.NotNull(result);
        Assert.False(string.IsNullOrEmpty(result.ImageUrl));
    }

    [Fact]
    public async Task UploadImage_InvalidExtension_Returns400()
    {
        var client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "blog-upload-bad@test.com");

        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(new byte[] { 0x00 });
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(fileContent, "file", "file.pdf");

        var response = await client.PostAsync("/api/blog/upload-image", content);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private record BlogPostSummaryResponse(Guid Id, string Title, string Slug, string? Excerpt, string? FeaturedImageUrl, string? Author, bool Published, DateTime? PublishedAtUtc, DateTime CreatedAtUtc);
    private record BlogPostResponse(Guid Id, string Title, string Slug, string Content, string? Excerpt, string? FeaturedImageUrl, string? Author, bool Published, DateTime? PublishedAtUtc, DateTime CreatedAtUtc);
    private record ImageUrlResponse(string ImageUrl);
}
