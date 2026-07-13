using System.Text;
using EdenRelics.SellerTool.Api;
using EdenRelics.SellerTool.Data;
using EdenRelics.SellerTool.Dating;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// JWT bearer auth — same signing key/issuer/audience as the main site, so a seller's existing token
// authorises them on the tool. (Token issuance / a tool-native login is a separate, later feature.)
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options => options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidateAudience = true,
        ValidAudience = builder.Configuration["Jwt:Audience"],
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"] ?? "")),
        ValidateLifetime = true,
    });
builder.Services.AddAuthorization();

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
