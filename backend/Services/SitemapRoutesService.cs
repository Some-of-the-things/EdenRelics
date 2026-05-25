using System.Text.Json;

namespace Eden_Relics_BE.Services;

public record SitemapRoute(string Path, string Changefreq, string Priority);

/// <summary>
/// Fetches the static sitemap route list from the frontend's deployed assets.
/// The frontend ships <c>public/sitemap-routes.json</c>, which becomes the single
/// source of truth for which static URLs make it into the sitemap. If the
/// frontend hasn't deployed a route, the backend can't advertise it — preventing
/// the failure mode where the sitemap promises URLs that 404.
/// </summary>
public class SitemapRoutesService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(1);
    private static readonly TimeSpan FetchTimeout = TimeSpan.FromSeconds(10);

    /// <summary>Minimal fallback if the frontend fetch fails — homepage only.</summary>
    private static readonly IReadOnlyList<SitemapRoute> MinimalFallback =
    [
        new SitemapRoute("/", "daily", "1.0"),
    ];

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly IHttpClientFactory _httpFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<SitemapRoutesService> _logger;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    private IReadOnlyList<SitemapRoute> _cached = MinimalFallback;
    private DateTime _cachedAtUtc = DateTime.MinValue;

    public SitemapRoutesService(
        IHttpClientFactory httpFactory,
        IConfiguration config,
        ILogger<SitemapRoutesService> logger)
    {
        _httpFactory = httpFactory;
        _config = config;
        _logger = logger;
    }

    public async Task<IReadOnlyList<SitemapRoute>> GetAsync()
    {
        if (DateTime.UtcNow - _cachedAtUtc < CacheTtl && _cachedAtUtc != DateTime.MinValue)
        {
            return _cached;
        }

        await _refreshLock.WaitAsync();
        try
        {
            if (DateTime.UtcNow - _cachedAtUtc < CacheTtl && _cachedAtUtc != DateTime.MinValue)
            {
                return _cached;
            }
            await RefreshAsync();
            return _cached;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private async Task RefreshAsync()
    {
        string frontendUrl = _config["Sitemap:FrontendUrl"] ?? "https://edenrelics.co.uk";
        string sourceUrl = $"{frontendUrl.TrimEnd('/')}/sitemap-routes.json";

        try
        {
            HttpClient client = _httpFactory.CreateClient();
            client.Timeout = FetchTimeout;
            HttpResponseMessage response = await client.GetAsync(sourceUrl);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Sitemap routes fetch from {Url} returned {Status}. Keeping previous cache (count={Count}).",
                    sourceUrl, response.StatusCode, _cached.Count);
                return;
            }

            string json = await response.Content.ReadAsStringAsync();
            List<SitemapRoute>? routes = JsonSerializer.Deserialize<List<SitemapRoute>>(json, JsonOpts);
            if (routes is null || routes.Count == 0)
            {
                _logger.LogWarning("Sitemap routes fetch from {Url} returned empty or invalid JSON.", sourceUrl);
                return;
            }

            _cached = routes;
            _cachedAtUtc = DateTime.UtcNow;
            _logger.LogInformation("Sitemap routes refreshed from {Url} ({Count} routes).", sourceUrl, routes.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Sitemap routes fetch from {Url} failed. Keeping previous cache.", sourceUrl);
        }
    }
}
