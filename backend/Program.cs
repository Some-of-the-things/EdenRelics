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

// Database
string? connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<EdenRelicsDbContext>(options =>
    options.UseNpgsql(connectionString!));

// Repositories
builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));

// Image optimization
builder.Services.AddSingleton<ImageOptimizationService>();

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
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(
                builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? ["http://localhost:4200"])
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

app.Run();

public partial class Program { }
