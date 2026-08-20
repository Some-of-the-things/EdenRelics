using EdenRelics.SellerTool.Api;

namespace EdenRelics.SellerTool.Api.Tests;

/// <summary>
/// Covers the DATABASE_URL -> Npgsql translation. Untested, it demanded TLS from a loopback
/// Postgres and failed the CI boot check on 2026-08-18.
/// </summary>
public class ToolConnectionStringTests
{
    [Theory]
    [InlineData("postgres://tool:tool@localhost:5432/tool")]
    [InlineData("postgres://tool:tool@127.0.0.1:5432/tool")]
    [InlineData("postgres://u:p@eden-relics-tool-db.flycast:5432/db")]
    [InlineData("postgres://u:p@eden-relics-tool-db.internal:5432/db")]
    public void DisablesSslForHostsReachedWithoutCrossingANetwork(string url)
    {
        string conn = ToolConnectionString.FromPostgresUrl(url);

        Assert.Contains("SSL Mode=Disable", conn);
        Assert.DoesNotContain("SSL Mode=Require", conn);
    }

    [Theory]
    [InlineData("postgres://u:p@db.example.com:5432/db")]
    [InlineData("postgres://u:p@1.2.3.4:5432/db")]
    public void RequiresSslForHostsReachedOverANetwork(string url)
    {
        string conn = ToolConnectionString.FromPostgresUrl(url);

        Assert.Contains("SSL Mode=Require", conn);
        Assert.Contains("Trust Server Certificate=true", conn);
    }

    [Fact]
    public void CarriesAcrossHostPortDatabaseAndCredentials()
    {
        string conn = ToolConnectionString.FromPostgresUrl("postgres://tool:s3cret@localhost:5441/eden_relics_tool");

        Assert.Contains("Host=localhost", conn);
        Assert.Contains("Port=5441", conn);
        Assert.Contains("Database=eden_relics_tool", conn);
        Assert.Contains("Username=tool", conn);
        Assert.Contains("Password=s3cret", conn);
    }

    [Fact]
    public void DefaultsThePortWhenTheUrlOmitsIt()
    {
        string conn = ToolConnectionString.FromPostgresUrl("postgres://u:p@db.example.com/db");

        Assert.Contains("Port=5432", conn);
    }

    [Fact]
    public void DropsAnSslModeTheUrlCarriedRatherThanEmittingItTwice()
    {
        // Fly appends sslmode to DATABASE_URL. We decide SSL Mode ourselves, and Npgsql rejects a
        // connection string that sets the same key twice.
        string conn = ToolConnectionString.FromPostgresUrl("postgres://u:p@db.example.com:5432/db?sslmode=disable&pool_max_conns=10");

        Assert.Single(conn.Split("SSL Mode="), s => s.StartsWith("Require"));
        Assert.DoesNotContain("sslmode", conn);
        Assert.Contains("pool_max_conns=10", conn);
    }
}
