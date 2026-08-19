namespace EdenRelics.SellerTool.Api;

/// <summary>
/// Turns Fly's <c>DATABASE_URL</c> (a <c>postgres://</c> URL) into an Npgsql connection string.
/// </summary>
/// <remarks>
/// Lives here rather than as a local function in <c>Program</c> so it can be tested directly. It
/// was untested when it demanded TLS from a loopback Postgres and failed the CI boot check.
/// </remarks>
public static class ToolConnectionString
{
    /// <summary>Hosts we reach without crossing a network, so TLS buys nothing.</summary>
    private static readonly string[] LoopbackHosts = ["localhost", "127.0.0.1", "::1"];

    public static string FromPostgresUrl(string url)
    {
        Uri uri = new(url);
        string[] userInfo = uri.UserInfo.Split(':');
        string query = uri.Query.TrimStart('?');

        // TLS is required for anything crossing a network, and pointless for anything that does not.
        // Fly's private hosts (.flycast/.internal) never leave Fly's network and loopback never
        // leaves the machine. Postgres in both places is routinely built without SSL support, and
        // Npgsql fails such a connection outright rather than downgrading.
        bool isPrivateHost = uri.Host.EndsWith(".flycast")
            || uri.Host.EndsWith(".internal")
            || LoopbackHosts.Contains(uri.Host);
        string sslMode = isPrivateHost ? "SSL Mode=Disable" : "SSL Mode=Require;Trust Server Certificate=true";

        string password = userInfo.Length > 1 ? userInfo[1] : "";
        string baseConn = $"Host={uri.Host};Port={(uri.Port > 0 ? uri.Port : 5432)};"
            + $"Database={uri.AbsolutePath.TrimStart('/')};Username={userInfo[0]};Password={password};{sslMode}";

        // We decide SSL Mode ourselves (above), so drop any the URL carried rather than emit a
        // duplicate key, which Npgsql rejects.
        string extraParams = string.Join('&', query.Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Where(p => !p.StartsWith("sslmode", StringComparison.OrdinalIgnoreCase)));
        return string.IsNullOrEmpty(extraParams) ? baseConn : $"{baseConn};{extraParams}";
    }
}
