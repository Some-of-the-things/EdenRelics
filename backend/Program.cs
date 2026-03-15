using System.Text;
using Eden_Relics_BE.Data;
using Eden_Relics_BE.Repositories;
using Eden_Relics_BE.Services;
using Fido2NetLib;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
});
builder.Services.AddHealthChecks();

// Database — prefer DATABASE_URL (Fly Postgres), fall back to ConnectionStrings:DefaultConnection
string connectionString = Environment.GetEnvironmentVariable("DATABASE_URL") is { Length: > 0 } databaseUrl
    ? ConvertPostgresUrl(databaseUrl)
    : builder.Configuration.GetConnectionString("DefaultConnection")!;
builder.Services.AddDbContext<EdenRelicsDbContext>(options =>
    options.UseNpgsql(connectionString));

// Repositories
builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));

// Image optimization
builder.Services.AddSingleton<ImageOptimizationService>();

// SEO rank checking
builder.Services.AddScoped<RankCheckerService>();
builder.Services.AddHostedService<RankCheckBackgroundService>();

// HttpClient for external OAuth token verification
builder.Services.AddHttpClient();

// Email (Resend)
builder.Services.AddOptions();
builder.Services.AddHttpClient<Resend.ResendClient>();
builder.Services.Configure<Resend.ResendClientOptions>(o =>
{
    o.ApiToken = builder.Configuration["Email:ResendApiKey"] ?? "";
});
builder.Services.AddTransient<Resend.IResend, Resend.ResendClient>();
builder.Services.AddTransient<IEmailService, EmailService>();

// FIDO2 / Passkeys
builder.Services.AddFido2(options =>
{
    options.ServerDomain = builder.Configuration["Fido2:ServerDomain"] ?? "localhost";
    options.ServerName = "Eden Relics";
    options.Origins = builder.Configuration.GetSection("Fido2:Origins").Get<HashSet<string>>()
        ?? ["http://localhost:4200", "http://localhost:4000"];
});
builder.Services.AddDistributedMemoryCache();

// JWT Authentication
string jwtKey = builder.Configuration["Jwt:Key"]!;
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

// CORS
string[] allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? ["https://edenrelics.co.uk"];
// Ensure production origin is always included
if (!allowedOrigins.Contains("https://edenrelics.co.uk"))
    allowedOrigins = [..allowedOrigins, "https://edenrelics.co.uk"];
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

WebApplication app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Auto-apply migrations (skip for InMemory/test databases)
using (IServiceScope scope = app.Services.CreateScope())
{
    EdenRelicsDbContext db = scope.ServiceProvider.GetRequiredService<EdenRelicsDbContext>();
    if (db.Database.IsRelational())
        db.Database.Migrate();
    else
        db.Database.EnsureCreated();
}

// Optimize any uncompressed uploaded images
await app.Services.GetRequiredService<ImageOptimizationService>().OptimizeExistingImagesAsync();

app.UseResponseCompression();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        ctx.Context.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
    }
});
app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/healthz");

app.Run();

static string ConvertPostgresUrl(string url)
{
    Uri uri = new(url);
    string[] userInfo = uri.UserInfo.Split(':');
    string query = uri.Query?.TrimStart('?') ?? "";
    bool isFlyInternal = uri.Host.EndsWith(".flycast") || uri.Host.EndsWith(".internal");
    string sslMode = isFlyInternal ? "SSL Mode=Disable" : "SSL Mode=Require;Trust Server Certificate=true";
    string baseConn = $"Host={uri.Host};Port={(uri.Port > 0 ? uri.Port : 5432)};Database={uri.AbsolutePath.TrimStart('/')};Username={userInfo[0]};Password={userInfo[1]};{sslMode}";
    // Strip any sslmode from the query params to avoid conflicts
    string extraParams = string.Join("&", (query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        .Where(p => !p.StartsWith("sslmode", StringComparison.OrdinalIgnoreCase)));
    return string.IsNullOrEmpty(extraParams) ? baseConn : $"{baseConn};{extraParams}";
}

public partial class Program { }
