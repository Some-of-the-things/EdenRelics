using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace EdenRelics.SellerTool.Data;

/// <summary>Design-time factory so `dotnet ef migrations` can build the context without a running
/// host or a live database (no connection is made — only the model is inspected).</summary>
public sealed class ToolDbContextFactory : IDesignTimeDbContextFactory<ToolDbContext>
{
    public ToolDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<ToolDbContext>()
            .UseNpgsql("Host=localhost;Database=tool_design")
            .Options);
}
