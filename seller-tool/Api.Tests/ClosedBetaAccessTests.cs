using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using EdenRelics.SellerTool.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace EdenRelics.SellerTool.Api.Tests;

/// <summary>
/// The closed beta: the tool is admin-only, and a customer is not let in.
///
/// This is its own suite because it is the only one that runs the gate CLOSED, which is how prod
/// runs. <see cref="ToolApiTests"/> opens it so the seller-facing behaviour underneath stays
/// covered — but that means nothing in that suite would notice the tool being open to every
/// customer with an account, which is exactly what happened: the Angular route carried an
/// adminGuard, the API carried none, and the guard was mistaken for the boundary.
///
/// Note the factory deliberately sets NO Tool:AdminOnly value. The default is the thing worth
/// testing: if opening the beta ever becomes the accident rather than the decision, this fails.
/// </summary>
public class ClosedBetaAccessTests : IClassFixture<ClosedBetaAccessTests.ClosedFactory>
{
    private const string TestKey = "ToolTestSigningKey_AtLeast32CharsLong!!";
    private const string Issuer = "tool-test-issuer";
    private const string Audience = "tool-test-audience";

    private readonly ClosedFactory _factory;

    public ClosedBetaAccessTests(ClosedFactory factory) => _factory = factory;

    public class ClosedFactory : WebApplicationFactory<Program>
    {
        private readonly string _dbName = "closedbeta_" + Guid.NewGuid();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = TestKey,
                ["Jwt:Issuer"] = Issuer,
                ["Jwt:Audience"] = Audience,
                // Tool:AdminOnly is intentionally absent — see the class comment.
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
            });
        }
    }

    private static string Token(Guid userId, params string[] roles)
    {
        List<Claim> claims = [new(ClaimTypes.NameIdentifier, userId.ToString())];
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));
        SigningCredentials creds = new(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestKey)), SecurityAlgorithms.HmacSha256);
        JwtSecurityToken token = new(Issuer, Audience, claims, expires: DateTime.UtcNow.AddHours(1), signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private HttpClient ClientAs(params string[] roles)
    {
        HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", Token(Guid.NewGuid(), roles));
        return client;
    }

    /// <summary>A signed-in shopper. The exact thing that must not reach the tool.</summary>
    private HttpClient CustomerClient() => ClientAs();

    private HttpClient AdminClient() => ClientAs("Admin");

    [Theory]
    [InlineData("/garments")]
    [InlineData("/capture-standard")]
    public async Task ACustomerCannotReadAnythingFromTheTool(string path)
    {
        Assert.Equal(HttpStatusCode.Forbidden, (await CustomerClient().GetAsync(path)).StatusCode);
    }

    [Fact]
    public async Task ACustomerCannotCreateAGarment()
    {
        HttpResponseMessage response = await CustomerClient()
            .PostAsJsonAsync("/garments", new { title = "Not mine to make" });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ACustomerCannotReportEvents()
    {
        // Left open, a customer could inflate the numbers the ten-seller gate is judged on.
        HttpResponseMessage response = await CustomerClient().PostAsJsonAsync("/events", new
        {
            events = new[] { new { kind = "ListingPublished", platform = "Vinted" } },
        });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AnUnauthenticatedRequestIsStillRefused()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateClient().GetAsync("/garments")).StatusCode);
    }

    [Fact]
    public async Task AnAdminIsLetStraightThrough()
    {
        // Teodora assesses the tool on an admin account, so closing the gate must not close it on
        // her. If this fails, the beta has locked out the person the beta is for.
        Assert.Equal(HttpStatusCode.OK, (await AdminClient().GetAsync("/garments")).StatusCode);

        HttpResponseMessage created = await AdminClient()
            .PostAsJsonAsync("/garments", new { title = "1970s wool dress" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
    }

    [Fact]
    public async Task TheHealthCheckStaysOpen()
    {
        // Fly's checks are unauthenticated; gating this would take the app down rather than secure it.
        Assert.Equal(HttpStatusCode.OK, (await _factory.CreateClient().GetAsync("/healthz")).StatusCode);
    }
}
