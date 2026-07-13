using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EdenRelics.SellerTool.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EdenRelics.SellerTool.Api.Tests;

public class ToolApiTests : IClassFixture<ToolApiTests.Factory>
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
    private readonly Factory _factory;

    public ToolApiTests(Factory factory) => _factory = factory;

    /// <summary>Hosts the API with the Postgres provider swapped for an in-memory one.</summary>
    public class Factory : WebApplicationFactory<Program>
    {
        private readonly string _dbName = "apitest_" + Guid.NewGuid();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
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
            });
        }
    }

    private static async Task<Guid> CreateGarmentAsync(HttpClient client, string title)
    {
        HttpResponseMessage res = await client.PostAsJsonAsync("/garments", new { title });
        res.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();
    }

    private static Task<HttpResponseMessage> AddEvidenceAsync(HttpClient client, Guid garmentId, string type, string feature) =>
        client.PostAsJsonAsync($"/garments/{garmentId}/evidence", new { type, feature });

    private static async Task SeedVerifiedRuleAsync(HttpClient client, object rule, string id)
    {
        (await client.PostAsJsonAsync("/rules", rule)).EnsureSuccessStatusCode();
        (await client.PostAsync($"/rules/{id}/verify", null)).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task CreateGarment_AddEvidence_ThenGet_ShowsProposedEvidence()
    {
        HttpClient client = _factory.CreateClient();
        Guid id = await CreateGarmentAsync(client, "Cut-label dress");
        (await AddEvidenceAsync(client, id, "CareLabel", "care.tumble-dry-symbol")).EnsureSuccessStatusCode();

        HttpResponseMessage res = await client.GetAsync($"/garments/{id}");
        res.EnsureSuccessStatusCode();
        JsonElement g = JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement;

        Assert.Equal("Cut-label dress", g.GetProperty("title").GetString());
        JsonElement ev = g.GetProperty("evidence")[0];
        Assert.Equal("care.tumble-dry-symbol", ev.GetProperty("feature").GetString());
        Assert.Equal("Proposed", ev.GetProperty("confirmation").GetString());   // machine default
    }

    [Fact]
    public async Task DateGarment_OverVerifiedRules_ReturnsWorkedExample_FlagsClaim_AndStoresEstimate()
    {
        HttpClient client = _factory.CreateClient();
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
        Assert.Equal(3, r.GetProperty("evidence").GetArrayLength());   // evidence chain returned

        // The estimate is persisted (as proposed).
        JsonElement g = JsonDocument.Parse(await (await client.GetAsync($"/garments/{id}")).Content.ReadAsStringAsync()).RootElement;
        JsonElement est = g.GetProperty("estimates")[0];
        Assert.Equal(1980, est.GetProperty("earliest").GetInt32());
        Assert.Equal("Proposed", est.GetProperty("confirmation").GetString());
    }

    [Fact]
    public async Task UnverifiedRule_DoesNotAffectDating_UntilVerified()
    {
        HttpClient client = _factory.CreateClient();
        // Add a rule but DON'T verify it.
        (await client.PostAsJsonAsync("/rules", new { id = "UNVER", feature = "care.x", notBefore = 1985, strength = "Hard", transitionLagMonths = 0 })).EnsureSuccessStatusCode();

        Guid id = await CreateGarmentAsync(client, "Test");
        await AddEvidenceAsync(client, id, "CareLabel", "care.x");

        JsonElement before = JsonDocument.Parse(
            await (await client.PostAsJsonAsync($"/garments/{id}/date", new { })).Content.ReadAsStringAsync()).RootElement;
        Assert.False(before.TryGetProperty("earliest", out JsonElement e1) && e1.ValueKind == JsonValueKind.Number);

        // Now verify it — dating should pick it up.
        (await client.PostAsync("/rules/UNVER/verify", null)).EnsureSuccessStatusCode();
        JsonElement after = JsonDocument.Parse(
            await (await client.PostAsJsonAsync($"/garments/{id}/date", new { })).Content.ReadAsStringAsync()).RootElement;
        Assert.Equal(1985, after.GetProperty("earliest").GetInt32());
    }

    [Fact]
    public async Task Healthz_Ok()
    {
        HttpClient client = _factory.CreateClient();
        HttpResponseMessage res = await client.GetAsync("/healthz");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }
}
