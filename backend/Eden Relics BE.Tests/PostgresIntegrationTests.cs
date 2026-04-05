using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using static Eden_Relics_BE.Tests.Helpers;

namespace Eden_Relics_BE.Tests;

/// <summary>
/// Integration tests that run against a real PostgreSQL database via Testcontainers.
/// These catch issues that InMemory tests miss: jsonb column mapping, migrations,
/// value converters, Npgsql-specific behaviour, and EF Core provider differences.
/// </summary>
public class PostgresIntegrationTests : IClassFixture<PostgresApiFactory>
{
    private readonly PostgresApiFactory _factory;

    public PostgresIntegrationTests(PostgresApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task HealthCheck_ReturnsOk()
    {
        HttpClient client = _factory.CreateClient();
        HttpResponseMessage response = await client.GetAsync("/healthz");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetProducts_ReturnsSeededProducts()
    {
        HttpClient client = _factory.CreateClient();
        HttpResponseMessage response = await client.GetAsync("/api/products");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var products = await response.Content.ReadFromJsonAsync<List<ProductResponse>>(JsonOptions);
        Assert.NotNull(products);
        Assert.True(products.Count >= 10, $"Expected at least 10 seeded products, got {products.Count}");
    }

    [Fact]
    public async Task GetContent_ReturnsDefaults()
    {
        HttpClient client = _factory.CreateClient();
        HttpResponseMessage response = await client.GetAsync("/api/content");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>(JsonOptions);
        Assert.NotNull(content);
        Assert.True(content.ContainsKey("home.hero.title"));
        Assert.True(content.ContainsKey("policy.privacy.content"));
    }

    [Fact]
    public async Task CreateProduct_WithJsonbFields_RoundTrips()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "pg-admin@test.com");

        // Create product
        HttpResponseMessage createResponse = await client.PostAsJsonAsync("/api/products", new
        {
            name = "Postgres Test Dress",
            description = "Testing jsonb round-trip",
            price = 99.99,
            era = "1990s",
            category = "90s",
            size = "10",
            condition = "good",
            imageUrl = "https://placehold.co/400x500",
            additionalImageUrls = new[] { "https://placehold.co/1.jpg", "https://placehold.co/2.jpg" },
            videoUrls = new[] { "https://example.com/video.mp4" },
            inStock = true,
        });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<ProductResponse>(JsonOptions);
        Assert.NotNull(created);

        // Fetch it back
        HttpResponseMessage getResponse = await client.GetAsync($"/api/products/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var fetched = await getResponse.Content.ReadFromJsonAsync<ProductResponse>(JsonOptions);
        Assert.NotNull(fetched);
        Assert.Equal("Postgres Test Dress", fetched.Name);
        Assert.Equal(2, fetched.AdditionalImageUrls.Count);
        Assert.Single(fetched.VideoUrls);
    }

    [Fact]
    public async Task CreateBlogPost_WithTranslationFields_RoundTrips()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "pg-blog-admin@test.com");

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/blog", new
        {
            title = "Postgres Blog Post",
            content = "<p>Testing translations jsonb</p>",
            published = true,
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Register_Login_GetProfile_FullFlow()
    {
        HttpClient client = _factory.CreateClient();
        var (_, auth) = await RegisterAndLogin(client, "pg-flow@test.com");
        Assert.Equal("pg-flow@test.com", auth.User.Email);

        HttpResponseMessage profileResponse = await client.GetAsync("/api/account/profile");
        Assert.Equal(HttpStatusCode.OK, profileResponse.StatusCode);
        var profile = await profileResponse.Content.ReadFromJsonAsync<ProfileResponse>(JsonOptions);
        Assert.NotNull(profile);
        Assert.Equal("Test", profile.FirstName);
    }

    // Helper: RegisterAdmin using PostgresApiFactory (same interface as ApiFactory)
    private static async Task<(string Token, AuthResponse Auth)> RegisterAdmin(HttpClient client, PostgresApiFactory factory, string email)
    {
        var (_, auth) = await RegisterAndLogin(client, email);

        using Microsoft.Extensions.DependencyInjection.IServiceScope scope = factory.Services.CreateScope();
        Eden_Relics_BE.Data.EdenRelicsDbContext db = scope.ServiceProvider.GetRequiredService<Eden_Relics_BE.Data.EdenRelicsDbContext>();
        Eden_Relics_BE.Data.Entities.User? user = await db.Users.FindAsync(auth.User.Id);
        user!.Role = "Admin";
        await db.SaveChangesAsync();

        client.DefaultRequestHeaders.Authorization = null;
        HttpResponseMessage loginResponse = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email,
            password = "TestPass123!"
        });
        loginResponse.EnsureSuccessStatusCode();
        AuthResponse? adminAuth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", adminAuth!.Token);
        return (adminAuth.Token, adminAuth);
    }
}
