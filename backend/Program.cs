using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Eden_Relics_BE.Data;
using Eden_Relics_BE.Data.Entities;
using Eden_Relics_BE.Data.Interceptors;
using Npgsql;
using Eden_Relics_BE.Repositories;
using Eden_Relics_BE.Services;
using Fido2NetLib;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Serialize enums (e.g. ProductStatus) as kebab-case strings rather than ints
        // so frontend payloads stay readable.
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(System.Text.Json.JsonNamingPolicy.KebabCaseLower));
    });
builder.Services.AddOpenApi();

// Allow large uploads; the real per-request cap is enforced in the upload
// controllers using UploadLimits.MaxUploadBytes (4GB, under R2's 5GB single-part PUT limit).
builder.Services.Configure<KestrelServerOptions>(options =>
{
    options.Limits.MaxRequestBodySize = null;
});
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = long.MaxValue;
    options.ValueLengthLimit = int.MaxValue;
});
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = ctx =>
    {
        ctx.ProblemDetails.Extensions.Remove("traceId");
    };
});

// Rate limiting (disabled in Testing environment)
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;
    bool isTesting = builder.Environment.EnvironmentName is "Testing" or "Development";
    options.AddPolicy("auth", httpContext =>
        isTesting
            ? RateLimitPartition.GetNoLimiter("none")
            : RateLimitPartition.GetFixedWindowLimiter(
                httpContext.Request.Headers["Fly-Client-IP"].FirstOrDefault()
                    ?? httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',')[0].Trim()
                    ?? httpContext.Connection.RemoteIpAddress?.ToString()
                    ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 5,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }));
    options.AddPolicy("contact", httpContext =>
        isTesting
            ? RateLimitPartition.GetNoLimiter("none")
            : RateLimitPartition.GetFixedWindowLimiter(
                httpContext.Request.Headers["Fly-Client-IP"].FirstOrDefault()
                    ?? httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',')[0].Trim()
                    ?? httpContext.Connection.RemoteIpAddress?.ToString()
                    ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 3,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }));
    // Generous cap for abuse-prone but legitimately repeatable public endpoints
    // (checkout retries, calendar clients polling the iCal feed).
    options.AddPolicy("public-write", httpContext =>
        isTesting
            ? RateLimitPartition.GetNoLimiter("none")
            : RateLimitPartition.GetFixedWindowLimiter(
                httpContext.Request.Headers["Fly-Client-IP"].FirstOrDefault()
                    ?? httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',')[0].Trim()
                    ?? httpContext.Connection.RemoteIpAddress?.ToString()
                    ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 20,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }));
});
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
});
builder.Services.AddHealthChecks();

// Database — prefer DATABASE_URL (Fly Postgres), fall back to ConnectionStrings:DefaultConnection
string connectionString = Environment.GetEnvironmentVariable("DATABASE_URL") is { Length: > 0 } databaseUrl
    ? ConvertPostgresUrl(databaseUrl)
    : builder.Configuration.GetConnectionString("DefaultConnection")!;
NpgsqlDataSourceBuilder dataSourceBuilder = new(connectionString);
dataSourceBuilder.EnableDynamicJson();
NpgsqlDataSource dataSource = dataSourceBuilder.Build();

builder.Services.AddDbContext<EdenRelicsDbContext>(options =>
    options.UseNpgsql(dataSource)
        .AddInterceptors(new SoftDeleteInterceptor())
        .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning)));

// Repositories
builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));

// Domain services (controllers stay thin; persistence goes through repositories)
builder.Services.AddScoped<IBlogService, BlogService>();
builder.Services.AddScoped<IOffsiteSaleService, OffsiteSaleService>();
builder.Services.AddScoped<IMailingListService, MailingListService>();
builder.Services.AddScoped<IBrandingService, BrandingService>();
builder.Services.AddScoped<IReviewService, ReviewService>();
builder.Services.AddScoped<IAdminUserService, AdminUserService>();
builder.Services.AddScoped<IContentService, ContentService>();
builder.Services.AddScoped<ICalendarFeedService, CalendarFeedService>();
builder.Services.AddScoped<ISitemapService, SitemapService>();
builder.Services.AddScoped<IMarketplaceService, MarketplaceService>();
builder.Services.AddScoped<ICalendarService, CalendarService>();
builder.Services.AddScoped<IAccountsService, AccountsService>();

// Image optimization & storage
builder.Services.AddSingleton<ImageOptimizationService>();
builder.Services.AddSingleton<ImageStorageService>();

// Cart interest tracking
builder.Services.AddSingleton<CartInterestService>();

// Translation
builder.Services.AddScoped<TranslationService>();
builder.Services.AddHostedService<TranslationBackgroundService>();

// SEO rank checking (now backed by GSC position data instead of Custom Search API)
builder.Services.AddScoped<RankCheckerService>();
builder.Services.AddHostedService<RankCheckBackgroundService>();

// Sitemap static-route list, sourced from the frontend's deployed assets
// so the backend can't advertise URLs the frontend hasn't shipped.
builder.Services.AddSingleton<SitemapRoutesService>();

// SEO health snapshots (catalog quality, sitemap counts, keyword positions)
builder.Services.AddSingleton<SeoHealthService>();
builder.Services.AddHostedService<SeoHealthBackgroundService>();

// Google Search Console + GA4 daily ingest
builder.Services.AddSingleton<GoogleSearchConsoleService>();
builder.Services.AddSingleton<GoogleAnalyticsService>();
builder.Services.AddHostedService<TrafficIngestBackgroundService>();

// Monzo bank integration
builder.Services.AddHttpClient<MonzoService>();
builder.Services.AddScoped<MonzoService>();
builder.Services.AddHostedService<MonzoSyncBackgroundService>();

// Regulatory-obligations calendar + operator reminders
builder.Services.Configure<LiabilityOptions>(builder.Configuration.GetSection(LiabilityOptions.SectionName));
builder.Services.AddScoped<ILiabilityScheduleService, LiabilityScheduleService>();
builder.Services.AddScoped<IObligationReminderSync, ObligationReminderSync>();
builder.Services.AddHostedService<LiabilityScheduleHostedService>();
builder.Services.AddHostedService<ReminderDispatcher>();

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
builder.Services.AddHttpClient<GeoIpService>();

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
builder.Services.AddSingleton<JwtTokenService>();
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
        // Revoke tokens whose version no longer matches the user's (e.g. after a
        // password change), and reject legacy tokens minted before token_version existed.
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async ctx =>
            {
                string? versionClaim = ctx.Principal?.FindFirst("token_version")?.Value;
                string? userIdStr = ctx.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (versionClaim is null || !int.TryParse(versionClaim, out int tokenVersion)
                    || userIdStr is null || !Guid.TryParse(userIdStr, out Guid userId))
                {
                    ctx.Fail("Invalid token.");
                    return;
                }

                EdenRelicsDbContext db = ctx.HttpContext.RequestServices.GetRequiredService<EdenRelicsDbContext>();
                User? user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
                if (user is null || user.TokenVersion != tokenVersion)
                {
                    ctx.Fail("Token has been revoked.");
                }
            }
        };
    });

// CORS
string[] allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? ["https://edenrelics.co.uk"];
// Ensure production origin is always included (but not in staging, which has its own origins)
if (builder.Environment.IsProduction() && !allowedOrigins.Contains("https://edenrelics.co.uk"))
{
    allowedOrigins = [..allowedOrigins, "https://edenrelics.co.uk"];
}
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
    {
        db.Database.Migrate();
    }
    else
    {
        db.Database.EnsureCreated();
    }

    // Backfill product slugs for any rows missing one (one-time, idempotent)
    List<Eden_Relics_BE.Data.Entities.Product> missing = db.Products
        .Where(p => p.Slug == "" || p.Slug == null!)
        .ToList();
    if (missing.Count > 0)
    {
        HashSet<string> taken = new(
            db.Products.Where(p => p.Slug != "" && p.Slug != null!).Select(p => p.Slug),
            StringComparer.OrdinalIgnoreCase);
        foreach (Eden_Relics_BE.Data.Entities.Product product in missing)
        {
            string baseSlug = Eden_Relics_BE.Services.SlugHelper.Generate(product.Name);
            if (string.IsNullOrEmpty(baseSlug))
            {
                baseSlug = product.Id.ToString();
            }
            string unique = Eden_Relics_BE.Services.SlugHelper.MakeUnique(baseSlug, taken);
            product.Slug = unique;
            taken.Add(unique);
        }
        db.SaveChanges();
    }
}

app.UseResponseCompression();
app.UseCors("AllowFrontend");

// Security headers (after CORS so preflight responses aren't blocked)
app.Use(async (context, next) =>
{
    context.Response.Headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=(), usb=(), serial=()";
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["Content-Security-Policy"] = "frame-ancestors 'none'";
    await next();
});
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        ctx.Context.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
    }
});
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// In staging, restrict all non-auth endpoints to Admin users only
if (app.Environment.IsEnvironment("Staging"))
{
    app.UseMiddleware<Eden_Relics_BE.Middleware.StagingAccessMiddleware>();
}

app.MapControllers();
app.MapHealthChecks("/healthz");

// Optimize images and backfill responsive variants in the background after the
// app starts accepting requests. Both calls are idempotent.
app.Lifetime.ApplicationStarted.Register(() =>
{
    _ = Task.Run(async () =>
    {
        ImageOptimizationService optimizer = app.Services.GetRequiredService<ImageOptimizationService>();
        await optimizer.OptimizeExistingImagesAsync();
        await optimizer.BackfillImageVariantsAsync();
    });
});

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
