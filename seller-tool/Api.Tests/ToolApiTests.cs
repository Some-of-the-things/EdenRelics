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
                // Open the beta gate for this suite, so the seller-facing behaviour it exists to
                // cover — owner scoping, a seller seeing only their own garments, an admin seeing
                // all — is exercised as a seller rather than as an admin, which would make those
                // assertions meaningless. The closed gate is covered in ClosedBetaAccessTests.
                ["Tool:AdminOnly"] = "false",
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

    // --- /dating/preview: the admin-facing "try the engine" path. Same engine, same rules, but it
    // must never write to the archive, because the archive is the asset.

    [Fact]
    public async Task DatingPreview_RunsTheEngine_AndReturnsTheFullEvidenceChain()
    {
        HttpClient client = AdminClient();
        await SeedVerifiedRuleAsync(client, new { id = "P-TD", feature = "preview.chain.dryer", notBefore = 1980, strength = "Hard", transitionLagMonths = 0, sourceCitation = "BS 2747:1980" }, "P-TD");
        await SeedVerifiedRuleAsync(client, new { id = "P-WT", feature = "preview.chain.tub", notAfter = 1986, strength = "Hard", transitionLagMonths = 0 }, "P-WT");

        HttpResponseMessage res = await client.PostAsJsonAsync("/dating/preview", new
        {
            evidence = new[]
            {
                new { feature = "preview.chain.dryer", type = "CareLabel", rawValue = (string?)null },
                new { feature = "preview.chain.tub", type = "CareLabel", rawValue = (string?)null },
            },
            claimEarliest = 1970,
            claimLatest = 1979,
        });
        res.EnsureSuccessStatusCode();
        JsonElement r = JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement;

        Assert.Equal(1980, r.GetProperty("earliest").GetInt32());
        Assert.Equal(1986, r.GetProperty("latest").GetInt32());
        Assert.Equal("Estimated", r.GetProperty("outcome").GetString());
        Assert.Equal("Hard", r.GetProperty("claimFlag").GetProperty("strength").GetString());

        JsonElement chain = r.GetProperty("evidence");
        Assert.Equal(2, chain.GetArrayLength());
        // The preview carries the reasoning, not just the answer.
        JsonElement first = chain[0];
        Assert.True(first.GetProperty("applied").GetBoolean());
        Assert.False(string.IsNullOrEmpty(first.GetProperty("provenance").GetString()));
        Assert.False(string.IsNullOrEmpty(first.GetProperty("bound").GetString()));
    }

    [Fact]
    public async Task DatingPreview_SurfacesAHardContradiction_RatherThanAveragingIt()
    {
        HttpClient client = AdminClient();
        await SeedVerifiedRuleAsync(client, new { id = "C-TD", feature = "preview.contra.dryer", notBefore = 1980, strength = "Hard", transitionLagMonths = 0 }, "C-TD");
        await SeedVerifiedRuleAsync(client, new { id = "C-CEY", feature = "preview.contra.ceylon", notAfter = 1972, strength = "Hard", transitionLagMonths = 0 }, "C-CEY");

        HttpResponseMessage res = await client.PostAsJsonAsync("/dating/preview", new
        {
            evidence = new[]
            {
                new { feature = "preview.contra.dryer", type = "CareLabel" },
                new { feature = "preview.contra.ceylon", type = "OriginText" },
            },
        });
        res.EnsureSuccessStatusCode();
        JsonElement r = JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement;

        Assert.Equal("HardContradiction", r.GetProperty("outcome").GetString());
    }

    [Fact]
    public async Task DatingPreview_WritesNothing_NoGarmentsNoEstimates()
    {
        HttpClient client = AdminClient();
        await SeedVerifiedRuleAsync(client, new { id = "S-TD", feature = "preview.stateless.dryer", notBefore = 1980, strength = "Hard", transitionLagMonths = 0 }, "S-TD");

        int garmentsBefore = JsonDocument.Parse(
            await (await client.GetAsync("/garments")).Content.ReadAsStringAsync()).RootElement.GetArrayLength();

        for (int i = 0; i < 3; i++)
        {
            (await client.PostAsJsonAsync("/dating/preview", new
            {
                evidence = new[] { new { feature = "care.tumble-dry-symbol", type = "CareLabel" } },
            })).EnsureSuccessStatusCode();
        }

        int garmentsAfter = JsonDocument.Parse(
            await (await client.GetAsync("/garments")).Content.ReadAsStringAsync()).RootElement.GetArrayLength();
        Assert.Equal(garmentsBefore, garmentsAfter);
    }

    [Fact]
    public async Task DatingPreview_WithNoObservations_IsRejected()
    {
        HttpClient client = AdminClient();
        HttpResponseMessage res = await client.PostAsJsonAsync("/dating/preview", new { evidence = Array.Empty<object>() });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task DatingPreview_AndFeatures_AreAdminOnly()
    {
        HttpClient seller = SellerClient(Guid.NewGuid());
        HttpResponseMessage preview = await seller.PostAsJsonAsync("/dating/preview", new
        {
            evidence = new[] { new { feature = "care.tumble-dry-symbol", type = "CareLabel" } },
        });
        Assert.Equal(HttpStatusCode.Forbidden, preview.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await seller.GetAsync("/dating/features")).StatusCode);
    }

    [Fact]
    public async Task DatingFeatures_ListsOnlyWhatTheLiveRulesCanActOn()
    {
        HttpClient client = AdminClient();
        await SeedVerifiedRuleAsync(client, new { id = "F-TD", feature = "preview.features.live", notBefore = 1980, strength = "Hard", transitionLagMonths = 0 }, "F-TD");
        // Unverified: must not be offered, or the picker would suggest a feature that does nothing.
        (await client.PostAsJsonAsync("/rules", new { id = "F-INERT", feature = "preview.features.inert", notBefore = 1990, strength = "Hard", transitionLagMonths = 0 })).EnsureSuccessStatusCode();

        JsonElement features = JsonDocument.Parse(
            await (await client.GetAsync("/dating/features")).Content.ReadAsStringAsync()).RootElement;

        List<string> codes = [.. features.EnumerateArray().Select(f => f.GetProperty("feature").GetString()!)];
        Assert.Contains("preview.features.live", codes);
        Assert.DoesNotContain("preview.features.inert", codes);
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

        // A real decodable image at or above the care-label floor: the capture standard validates
        // before storing, so four arbitrary bytes are now (correctly) refused as unreadable.
        using MultipartFormDataContent content = new();
        ByteArrayContent file = new(TestImage(1400, 1400));
        file.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        content.Add(file, "file", "care-label.jpg");
        content.Add(new StringContent("CareLabel"), "type");
        content.Add(new StringContent("care.tumble-dry-symbol"), "feature");
        content.Add(new StringContent("CareLabel"), "slot");
        content.Add(new StringContent("true"), "archiveRights");

        HttpResponseMessage res = await client.PostAsync($"/garments/{id}/capture", content);
        res.EnsureSuccessStatusCode();
        JsonElement body = JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement;
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("imageKey").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("displayImageKey").GetString()));

        JsonElement g = JsonDocument.Parse(await (await client.GetAsync($"/garments/{id}")).Content.ReadAsStringAsync()).RootElement;
        JsonElement ev = g.GetProperty("evidence")[0];
        Assert.False(string.IsNullOrWhiteSpace(ev.GetProperty("imageKey").GetString()));
        Assert.Equal("Proposed", ev.GetProperty("confirmation").GetString());
    }

    /// <summary>The upload must be refused without a per-capture rights grant.</summary>
    [Fact]
    public async Task Capture_WithoutArchiveRights_IsRefused()
    {
        HttpClient client = SellerClient(Guid.NewGuid());
        Guid id = await CreateGarmentAsync(client, "Labelled dress");

        using MultipartFormDataContent content = new();
        ByteArrayContent file = new(TestImage(1400, 1400));
        file.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        content.Add(file, "file", "care-label.jpg");
        content.Add(new StringContent("CareLabel"), "type");
        content.Add(new StringContent("care.tumble-dry-symbol"), "feature");
        content.Add(new StringContent("CareLabel"), "slot");

        HttpResponseMessage res = await client.PostAsync($"/garments/{id}/capture", content);

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        JsonElement body = JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("rights_not_granted", body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task CaptureStandard_IsServedSoTheClientDoesNotDuplicateIt()
    {
        HttpClient client = SellerClient(Guid.NewGuid());

        JsonElement body = JsonDocument.Parse(
            await (await client.GetAsync("/capture-standard")).Content.ReadAsStringAsync()).RootElement;

        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("version").GetString()));
        JsonElement slots = body.GetProperty("slots");
        Assert.True(slots.GetArrayLength() > 0);
        // Required slots come first and carry their own resolution floor and guidance.
        Assert.Equal("CareLabel", slots[0].GetProperty("slot").GetString());
        Assert.True(slots[0].GetProperty("required").GetBoolean());
        Assert.Equal(1200, slots[0].GetProperty("minimumLongEdge").GetInt32());
    }

    /// <summary>A decodable JPEG of the requested size, for tests that must clear the standard.</summary>
    private static byte[] TestImage(int width, int height)
    {
        using SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32> img = new(width, height);
        using MemoryStream ms = new();
        img.Save(ms, new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder());
        return ms.ToArray();
    }

    [Fact]
    public async Task ListGarments_IsOwnerScoped_AndSummarisesEvidence()
    {
        Guid ownerId = Guid.NewGuid();
        HttpClient owner = SellerClient(ownerId);
        Guid id = await CreateGarmentAsync(owner, "My dress");
        (await AddEvidenceAsync(owner, id, "CareLabel", "care.tumble-dry-symbol")).EnsureSuccessStatusCode();

        // A different seller has their own (empty) list and never sees the owner's garment.
        HttpClient other = SellerClient(Guid.NewGuid());
        JsonElement otherList = JsonDocument.Parse(await (await other.GetAsync("/garments")).Content.ReadAsStringAsync()).RootElement;
        Assert.Equal(0, otherList.GetArrayLength());

        // The owner sees exactly their garment, with an evidence count.
        JsonElement ownerList = JsonDocument.Parse(await (await owner.GetAsync("/garments")).Content.ReadAsStringAsync()).RootElement;
        Assert.Equal(1, ownerList.GetArrayLength());
        Assert.Equal("My dress", ownerList[0].GetProperty("title").GetString());
        Assert.Equal(1, ownerList[0].GetProperty("evidenceCount").GetInt32());
    }

    [Fact]
    public async Task ListGarments_AsAdmin_SeesAllOwners()
    {
        await CreateGarmentAsync(SellerClient(Guid.NewGuid()), "Seller A dress");
        await CreateGarmentAsync(SellerClient(Guid.NewGuid()), "Seller B dress");

        JsonElement adminList = JsonDocument.Parse(await (await AdminClient().GetAsync("/garments")).Content.ReadAsStringAsync()).RootElement;
        Assert.True(adminList.GetArrayLength() >= 2);
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

    // ---- Instrumentation (brief §10) ----

    [Fact]
    public async Task Events_AreRecordedForTheCaller_AndSummarisedForAdmins()
    {
        HttpClient seller = SellerClient(Guid.NewGuid());
        HttpResponseMessage res = await seller.PostAsJsonAsync("/events", new
        {
            events = new[]
            {
                new { kind = "MeasurementProposed", platform = (string?)null, durationMs = (int?)null, detail = (string?)null },
                new { kind = "MeasurementAccepted", platform = (string?)null, durationMs = (int?)null, detail = (string?)null },
            },
        });

        Assert.Equal(HttpStatusCode.Accepted, res.StatusCode);

        MetricsSummaryDto? summary = JsonSerializer.Deserialize<MetricsSummaryDto>(
            await (await AdminClient().GetAsync("/metrics/summary?days=7")).Content.ReadAsStringAsync(), Json);

        Assert.NotNull(summary);
        Assert.True(summary!.Measurement.Accepted >= 1);
        Assert.True(summary.WeeklyActiveSellers >= 1);
    }

    [Fact]
    public async Task Events_RefuseServerOwnedKinds_SoTheFlagRateCannotBeInflated()
    {
        HttpClient seller = SellerClient(Guid.NewGuid());

        HttpResponseMessage res = await seller.PostAsJsonAsync("/events", new
        {
            events = new[] { new { kind = "DatingFlagRaised" } },
        });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Events_RejectAnUnknownKind_RatherThanDroppingItSilently()
    {
        HttpResponseMessage res = await SellerClient(Guid.NewGuid()).PostAsJsonAsync("/events", new
        {
            events = new[] { new { kind = "SomethingWeNeverDefined" } },
        });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Events_RequireAuthentication()
    {
        HttpResponseMessage res = await _factory.CreateClient().PostAsJsonAsync("/events", new
        {
            events = new[] { new { kind = "ListingPublished" } },
        });

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task MetricsSummary_IsAdminOnly()
    {
        Assert.Equal(HttpStatusCode.Forbidden,
            (await SellerClient(Guid.NewGuid()).GetAsync("/metrics/summary")).StatusCode);
    }

    [Fact]
    public async Task DatingAGarment_RecordsTheFlagItself_NotLeavingItToTheClient()
    {
        HttpClient admin = AdminClient();
        await SeedVerifiedRuleAsync(admin, new
        {
            id = "flag-metric-tumble",
            feature = "care.tumble-dry-symbol",
            type = "CareLabel",
            notBefore = 1980,
            strength = "Hard",
            transitionLagMonths = 0,
            sourceCitation = "test",
        }, "flag-metric-tumble");

        Guid garmentId = await CreateGarmentAsync(admin, "Allegedly 1970s dress");
        (await AddEvidenceAsync(admin, garmentId, "CareLabel", "care.tumble-dry-symbol")).EnsureSuccessStatusCode();

        int raisedBefore = JsonSerializer.Deserialize<MetricsSummaryDto>(
            await admin.GetStringAsync("/metrics/summary?days=1"), Json)!.DatingFlags.Raised;

        // Claimed 1975, but the evidence cannot predate 1980.
        HttpResponseMessage res = await admin.PostAsJsonAsync($"/garments/{garmentId}/date", new
        {
            claimEarliest = 1970,
            claimLatest = 1979,
        });
        res.EnsureSuccessStatusCode();

        MetricsSummaryDto after = JsonSerializer.Deserialize<MetricsSummaryDto>(
            await admin.GetStringAsync("/metrics/summary?days=1"), Json)!;

        Assert.Equal(raisedBefore + 1, after.DatingFlags.Raised);
    }
    // --- Bulk upload from the camera roll, and the zip rule (v1 reframe) ---

    private static ByteArrayContent JpegPart(int width, int height)
    {
        ByteArrayContent part = new(TestImage(width, height));
        part.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        return part;
    }

    [Fact]
    public async Task BulkUpload_StoresManyPhotosInOnePass()
    {
        HttpClient client = SellerClient(Guid.NewGuid());
        Guid id = await CreateGarmentAsync(client, "Back catalogue dress");

        using MultipartFormDataContent content = new();
        for (int i = 0; i < 3; i++)
        {
            content.Add(JpegPart(1400, 1400), "files", $"label-{i}.jpg");
        }
        content.Add(new StringContent("CareLabel"), "type");
        content.Add(new StringContent("care.wash-symbol"), "feature");
        content.Add(new StringContent("true"), "archiveRights");

        HttpResponseMessage res = await client.PostAsync($"/garments/{id}/captures", content);
        res.EnsureSuccessStatusCode();
        JsonElement body = JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement;

        Assert.Equal(3, body.GetProperty("uploaded").GetInt32());
        Assert.Equal(3, body.GetProperty("stored").GetInt32());
    }

    [Fact]
    public async Task BulkUpload_DefaultsToHistorical_SoTheBackCatalogueIsNeverMarkedStandardQuality()
    {
        HttpClient client = SellerClient(Guid.NewGuid());
        Guid id = await CreateGarmentAsync(client, "Back catalogue dress");

        using MultipartFormDataContent content = new();
        content.Add(JpegPart(1400, 1400), "files", "old-label.jpg");
        content.Add(new StringContent("CareLabel"), "type");
        content.Add(new StringContent("care.wash-symbol"), "feature");
        content.Add(new StringContent("true"), "archiveRights");

        HttpResponseMessage res = await client.PostAsync($"/garments/{id}/captures", content);
        res.EnsureSuccessStatusCode();
        JsonElement body = JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement;

        Assert.Equal("HistoricalUpload", body.GetProperty("results")[0].GetProperty("provenance").GetString());
    }

    [Fact]
    public async Task BulkUpload_OneBadPhotoDoesNotLoseTheRest()
    {
        // A hundred-photo import where the ninth is a screenshot must still store the other
        // ninety-nine. Partial is the normal case here, not an error state.
        HttpClient client = SellerClient(Guid.NewGuid());
        Guid id = await CreateGarmentAsync(client, "Mixed bag");

        using MultipartFormDataContent content = new();
        content.Add(JpegPart(1400, 1400), "files", "good.jpg");
        ByteArrayContent junk = new([1, 2, 3, 4]);
        junk.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        content.Add(junk, "files", "not-an-image.jpg");
        content.Add(JpegPart(1400, 1400), "files", "also-good.jpg");
        content.Add(new StringContent("CareLabel"), "type");
        content.Add(new StringContent("care.wash-symbol"), "feature");
        content.Add(new StringContent("true"), "archiveRights");

        HttpResponseMessage res = await client.PostAsync($"/garments/{id}/captures", content);
        res.EnsureSuccessStatusCode();
        JsonElement body = JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement;

        Assert.Equal(2, body.GetProperty("stored").GetInt32());
        Assert.Equal(1, body.GetProperty("skipped").GetInt32());
        Assert.Contains(body.GetProperty("results").EnumerateArray(), r => !r.GetProperty("stored").GetBoolean());
    }

    [Fact]
    public async Task AZipCannotBeLoggedWithoutSayingWhetherItIsOriginal()
    {
        HttpClient client = SellerClient(Guid.NewGuid());
        Guid id = await CreateGarmentAsync(client, "Zipped dress");

        HttpResponseMessage res = await client.PostAsJsonAsync($"/garments/{id}/evidence", new
        {
            type = "Zip",
            feature = "zip.maker-mark",
            rawValue = "YKK",
        });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("zip_originality_required", await res.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task AZipLoggedAsUnsureIsAccepted()
    {
        // "Unsure" is a legitimate answer and must always be available - forcing a guess is how
        // bad data gets into the corpus.
        HttpClient client = SellerClient(Guid.NewGuid());
        Guid id = await CreateGarmentAsync(client, "Zipped dress");

        HttpResponseMessage res = await client.PostAsJsonAsync($"/garments/{id}/evidence", new
        {
            type = "Zip",
            feature = "zip.maker-mark",
            zipOriginality = "Unsure",
        });

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
    }

    [Fact]
    public async Task NonZipEvidenceIsUnaffectedByTheZipRule()
    {
        HttpClient client = SellerClient(Guid.NewGuid());
        Guid id = await CreateGarmentAsync(client, "Plain dress");

        HttpResponseMessage res = await client.PostAsJsonAsync($"/garments/{id}/evidence", new
        {
            type = "CareLabel",
            feature = "care.wash-symbol",
        });

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
    }
}