using EdenRelics.SellerTool.Api;
using EdenRelics.SellerTool.Data;
using EdenRelics.SellerTool.Dating;
using Microsoft.EntityFrameworkCore;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Persistence: the tool's own Postgres DB (separate from the shop). Tests replace this with an
// in-memory provider. TODO before deploy: swap EnsureCreated (below) for EF migrations.
builder.Services.AddDbContext<ToolDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("ToolDb")));

// The dating engine reads its rules from the database, so rules update without a redeploy.
builder.Services.AddScoped<IRuleStore, DbRuleStore>();
builder.Services.AddScoped<IDatingEngine, DatingEngine>();

WebApplication app = builder.Build();

// Create the schema on a fresh relational DB (skipped for the in-memory test provider).
using (IServiceScope scope = app.Services.CreateScope())
{
    ToolDbContext db = scope.ServiceProvider.GetRequiredService<ToolDbContext>();
    if (db.Database.IsRelational())
    {
        db.Database.EnsureCreated();
    }
}

app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));
app.MapToolEndpoints();

app.Run();

/// <summary>Exposed so the integration tests can spin up the host via WebApplicationFactory.</summary>
public partial class Program;
