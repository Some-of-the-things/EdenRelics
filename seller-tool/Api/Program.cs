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

// Persistence: the tool's own Postgres DB (separate from the shop). Tests replace this with an
// in-memory provider. TODO before deploy: swap EnsureCreated (below) for EF migrations.
builder.Services.AddDbContext<ToolDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("ToolDb")));

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

/// <summary>Exposed so the integration tests can spin up the host via WebApplicationFactory.</summary>
public partial class Program;
