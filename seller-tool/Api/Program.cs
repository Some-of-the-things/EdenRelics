using System.Text;
using EdenRelics.SellerTool.Api;
using EdenRelics.SellerTool.Data;
using EdenRelics.SellerTool.Dating;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// JWT bearer auth — the tool accepts tokens minted by the main site so a seller's existing login
// authorises them here (token issuance / a tool-native login is a separate, later feature). The main
// site runs in more than one environment (prod and staging) with DIFFERENT signing keys, issuers, and
// audiences, so the tool validates against ALL of them: Jwt:Key/Issuer/Audience is the primary set and
// Jwt:Key2/Issuer2/Audience2 an optional second (e.g. staging). A token is valid if it matches any.
// NB: read config INSIDE the options lambda — it runs after the host's configuration is finalised,
// which the test host (and env-var/secret layering) depends on.
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        IConfiguration cfg = builder.Configuration;
        string[] validIssuers = [.. new[] { cfg["Jwt:Issuer"], cfg["Jwt:Issuer2"] }.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s!)];
        string[] validAudiences = [.. new[] { cfg["Jwt:Audience"], cfg["Jwt:Audience2"] }.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s!)];
        SymmetricSecurityKey[] signingKeys =
            [.. new[] { cfg["Jwt:Key"], cfg["Jwt:Key2"] }.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => new SymmetricSecurityKey(Encoding.UTF8.GetBytes(s!)))];

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuers = validIssuers,
            ValidateAudience = true,
            ValidAudiences = validAudiences,
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = signingKeys,
            TryAllIssuerSigningKeys = true,
            ValidateLifetime = true,
        };
    });
builder.Services.AddAuthorization();

// CORS — the tool is called from the Angular front-end (a different origin) with a bearer token in
// the Authorization header, which triggers a preflight. Allow the known front-end origins (overridable
// via Cors:AllowedOrigins). No credentials/cookies are used, so we don't enable AllowCredentials.
string[] allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() is { Length: > 0 } configured
    ? configured
    : ["https://edenrelics.co.uk", "https://www.edenrelics.co.uk", "https://staging.edenrelics.co.uk", "http://localhost:4200"];
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins(allowedOrigins)
    .AllowAnyHeader()
    .WithMethods("GET", "POST", "OPTIONS")));

// Persistence: prefer Fly's DATABASE_URL (postgres:// -> Npgsql), else ConnectionStrings:ToolDb.
// Tests replace this registration with an in-memory provider; startup Migrate()s a relational DB.
string toolConnection = Environment.GetEnvironmentVariable("DATABASE_URL") is { Length: > 0 } dbUrl
    ? ConvertPostgresUrl(dbUrl)
    : builder.Configuration.GetConnectionString("ToolDb") ?? "";
builder.Services.AddDbContext<ToolDbContext>(options => options.UseNpgsql(toolConnection));

// The dating engine reads its rules from the database, so rules update without a redeploy.
builder.Services.AddScoped<IRuleStore, DbRuleStore>();
builder.Services.AddScoped<IDatingEngine, DatingEngine>();

// Captured label/flat-lay images -> Cloudflare R2 (the archive/moat). Tests replace the store.
builder.Services.Configure<R2Options>(builder.Configuration.GetSection(R2Options.SectionName));
builder.Services.AddScoped<IImageStore, R2ImageStore>();

WebApplication app = builder.Build();

// Apply EF migrations on startup for a relational DB (skipped for the in-memory test provider).
using (IServiceScope scope = app.Services.CreateScope())
{
    ToolDbContext db = scope.ServiceProvider.GetRequiredService<ToolDbContext>();
    if (db.Database.IsRelational())
    {
        db.Database.Migrate();
    }
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));
app.MapToolEndpoints();

app.Run();

// Convert a postgres:// URL (Fly's DATABASE_URL) into an Npgsql connection string.
static string ConvertPostgresUrl(string url)
{
    Uri uri = new(url);
    string[] userInfo = uri.UserInfo.Split(':');
    string query = uri.Query.TrimStart('?');
    bool isFlyInternal = uri.Host.EndsWith(".flycast") || uri.Host.EndsWith(".internal");
    string sslMode = isFlyInternal ? "SSL Mode=Disable" : "SSL Mode=Require;Trust Server Certificate=true";
    string baseConn = $"Host={uri.Host};Port={(uri.Port > 0 ? uri.Port : 5432)};Database={uri.AbsolutePath.TrimStart('/')};Username={userInfo[0]};Password={userInfo[1]};{sslMode}";
    string extraParams = string.Join('&', query.Split('&', StringSplitOptions.RemoveEmptyEntries)
        .Where(p => !p.StartsWith("sslmode", StringComparison.OrdinalIgnoreCase)));
    return string.IsNullOrEmpty(extraParams) ? baseConn : $"{baseConn};{extraParams}";
}

/// <summary>Exposed so the integration tests can spin up the host via WebApplicationFactory.</summary>
public partial class Program;
