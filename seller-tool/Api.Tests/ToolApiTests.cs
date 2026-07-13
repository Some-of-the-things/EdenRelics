using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using EdenRelics.SellerTool.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace EdenRelics.SellerTool.Api.Tests;

public class ToolApiTests : IClassFixture<ToolApiTests.Factory>
{
    private const string TestKey = "ToolTestSigningKey_AtLeast32CharsLong!!";
    private const string Issuer = "tool-test-issuer";
    private const string Audience = "tool-test-audience";

    private readonly Factory _factory;

    public ToolApiTests(Factory factory) => _factory = factory;

    public class Factory : WebApplicationFactory<Program>
    {
        private readonly string _dbName = "apitest_" + Guid.NewGuid();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = TestKey,
                ["Jwt:Issuer"] = Issuer,
                ["Jwt:Audience"] = Audience,
            }));

            builder.ConfigureServices(services =>
            {
                List<ServiceDescriptor> ef = services.Where(d =>
                    d.ServiceType.FullName?.Contains("EntityFrameworkCore") == true
                    || d.ServiceType.FullName?.Contains("Npgsql") == true
                    || d.ServiceType == typeof(ToolDbContext)
                    || d.ServiceType == typeof(DbContextOptions<ToolDbContext>)).ToList();
                foreach (ServiceDescriptor d in ef)
                {
                    services.Remove(d);
                }
                services.AddDbContext<ToolDbContext>(o => o.UseInMemoryDatabase(_dbName));

                ServiceDescriptor? img = services.SingleOrDefault(d => d.ServiceType == typeof(IImageStore));
                if (img is not null)
                {
                    services.Remove(img);
                }
                services.AddScoped<IImageStore, FakeImageStore>();
            });
        }
    }

    private sealed class FakeImageStore : IImageStore
    {
        public Task<string> PutAsync(Stream content, string contentType, string keyPrefix, CancellationToken ct = default) =>
            Task.FromResult($"{keyPrefix}/fake-{Guid.NewGuid():N}.jpg");
    }

    private static string Token(Guid userId, params string[] roles)
    {
        List<Claim> claims = [new(ClaimTypes.NameIdentifier, userId.ToString())];
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));
        SigningCredentials creds = new(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestKey)), SecurityAlgorithms.HmacSha256);
        JwtSecurityToken token = new(Issuer, Audience, claims, expires: DateTime.UtcNow.AddHours(1), signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private HttpClient ClientAs(Guid userId, params string[] roles)
    {
        HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token(userId, roles));
        return client;
    }

    private HttpClient AdminClient() => ClientAs(Guid.NewGuid(), "Admin");
    private HttpClient SellerClient(Guid userId) => ClientAs(userId);

    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    private static async Task<Guid> CreateGarmentAsync(HttpClient client, string title)
    {
        HttpResponseMessage res = await client.PostAsJsonAsync("/garments", new { title });
        res.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();
    }

    private static Task<HttpResponseMessage> AddEvidenceAsync(HttpClient client, Guid garmentId, string type, string feature) =>
        client.PostAsJsonAsync($"/garments/{garmentId}/evidence", new { type, feature });

    private static async Task SeedVerifiedRuleAsync(HttpClient admin, object rule, string id)
    {
        (await admin.PostAsJsonAsync("/rules", rule)).EnsureSuccessStatusCode();
        (await admin.PostAsync($"/rules/{id}/verify", null)).EnsureSuccessStatusCode();
    }

    // ---- Functional ----

    [Fact]
    public async Task CreateGarment_AddEvidence_ThenGet_ShowsProposedEvidence()
    {
        HttpClient client = SellerClient(Guid.NewGuid());
        Guid id = await CreateGarmentAsync(client, "Cut-label dress");
        (await AddEvidenceAsync(client, id, "CareLabel", "care.tumble-dry-symbol")).EnsureSuccessStatusCode();

        JsonElement g = JsonDocument.Parse(await (await client.GetAsync($"/garments/{id}")).Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("Cut-label dress", g.GetProperty("title").GetString());
        JsonElement ev = g.GetProperty("evidence")[0];
        Assert.Equal("care.tumble-dry-symbol", ev.GetProperty("feature").GetString());
        Assert.Equal("Proposed", ev.GetProperty("confirmation").GetString());
    }

    [Fact]
    public async Task DateGarment_OverVerifiedRules_ReturnsWorkedExample_FlagsClaim_AndStoresEstimate()
    {
        HttpClient client = AdminClient();
        await SeedVerifiedRuleAsync(client, new { id = "CARE-TD", feature = "care.tumble-dry-symbol", notBefore = 1980, strength = "Hard", transitionLagMonths = 0 }, "CARE-TD");
        await SeedVerifiedRuleAsync(client, new { id = "CARE-WT", feature = "care.numbered-wash-tub", notAfter = 1986, strength = "Hard", transitionLagMonths = 0 }, "CARE-WT");
        await SeedVerifiedRuleAsync(client, new { id = "PHONE-01", feature = "phone.london-01", notAfter = 1990, strength = "Hard", transitionLagMonths = 0 }, "PHONE-01");

        Guid id = await CreateGarmentAsync(client, "Cut-label dress");
        await AddEvidenceAsync(client, id, "CareLabel", "care.tumble-dry-symbol");
        await AddEvidenceAsync(client, id, "CareLabel", "care.numbered-wash-tub");
        await AddEvidenceAsync(client, id, "PhoneNumber", "phone.london-01");

        HttpResponseMessage res = await client.PostAsJsonAsync($"/garments/{id}/date", new { claimEarliest = 1970, claimLatest = 1979 });
        res.EnsureSuccessStatusCode();
        JsonElement r = JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement;

        Assert.Equal(1980, r.GetProperty("earliest").GetInt32());
        Assert.Equal(1986, r.GetProperty("latest").GetInt32());
        Assert.Equal("Hard", r.GetProperty("claimFlag").GetProperty("strength").GetString());
        Assert.Equal(3, r.GetProperty("evidence").GetArrayLength());
    }

    [Fact]
    public async Task UnverifiedRule_DoesNotAffectDating_UntilVerified()
    {
        HttpClient client = AdminClient();
        (await client.PostAsJsonAsync("/rules", new { id = "UNVER", feature = "care.x", notBefore = 1985, strength = "Hard", transitionLagMonths = 0 })).EnsureSuccessStatusCode();

        Guid id = await CreateGarmentAsync(client, "Test");
        await AddEvidenceAsync(client, id, "CareLabel", "care.x");

        JsonElement before = JsonDocument.Parse(await (await client.PostAsJsonAsync($"/garments/{id}/date", new { })).Content.ReadAsStringAsync()).RootElement;
        Assert.False(before.TryGetProperty("earliest", out JsonElement e1) && e1.ValueKind == JsonValueKind.Number);

        (await client.PostAsync("/rules/UNVER/verify", null)).EnsureSuccessStatusCode();
        JsonElement after = JsonDocument.Parse(await (await client.PostAsJsonAsync($"/garments/{id}/date", new { })).Content.ReadAsStringAsync()).RootElement;
        Assert.Equal(1985, after.GetProperty("earliest").GetInt32());
    }

    [Fact]
    public async Task Capture_UploadsImage_CreatesProposedEvidenceWithKey()
    {
        HttpClient client = SellerClient(Guid.NewGuid());
        Guid id = await CreateGarmentAsync(client, "Labelled dress");

        using MultipartFormDataContent content = new();
        ByteArrayContent file = new([1, 2, 3, 4]);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        content.Add(file, "file", "care-label.jpg");
        content.Add(new StringContent("CareLabel"), "type");
        content.Add(new StringContent("care.tumble-dry-symbol"), "feature");

        HttpResponseMessage res = await client.PostAsync($"/garments/{id}/capture", content);
        res.EnsureSuccessStatusCode();
        JsonElement body = JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement;
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("imageKey").GetString()));

        JsonElement g = JsonDocument.Parse(await (await client.GetAsync($"/garments/{id}")).Content.ReadAsStringAsync()).RootElement;
        JsonElement ev = g.GetProperty("evidence")[0];
        Assert.False(string.IsNullOrWhiteSpace(ev.GetProperty("imageKey").GetString()));
        Assert.Equal("Proposed", ev.GetProperty("confirmation").GetString());
    }

    // ---- Auth enforcement ----

    [Fact]
    public async Task Anonymous_IsUnauthorized()
    {
        HttpClient anon = _factory.CreateClient();
        HttpResponseMessage res = await anon.PostAsJsonAsync("/garments", new { title = "x" });
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task NonAdmin_CannotManageRules()
    {
        HttpClient seller = SellerClient(Guid.NewGuid());
        HttpResponseMessage res = await seller.PostAsJsonAsync("/rules", new { id = "X", feature = "f", transitionLagMonths = 0 });
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Garment_IsOwnerScoped()
    {
        Guid ownerId = Guid.NewGuid();
        HttpClient owner = SellerClient(ownerId);
        Guid id = await CreateGarmentAsync(owner, "Private dress");

        // A different seller cannot see it.
        HttpClient other = SellerClient(Guid.NewGuid());
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/garments/{id}")).StatusCode);

        // The owner can.
        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync($"/garments/{id}")).StatusCode);
    }

    [Fact]
    public async Task Healthz_Ok_Anonymous()
    {
        HttpResponseMessage res = await _factory.CreateClient().GetAsync("/healthz");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }
}
