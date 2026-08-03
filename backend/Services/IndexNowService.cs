using System.Net.Http.Json;
using Microsoft.Extensions.Options;

namespace Eden_Relics_BE.Services;

public class IndexNowOptions
{
    public const string SectionName = "IndexNow";

    /// <summary>
    /// Public ownership token, not a secret — it is published verbatim at
    /// <c>https://{Host}/{Key}.txt</c>, which is the whole mechanism by which a search engine
    /// checks we are allowed to submit for this host. Committing it is correct.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    public string Host { get; set; } = "edenrelics.co.uk";

    /// <summary>Off by default so no environment starts pinging search engines by accident.</summary>
    public bool Enabled { get; set; }

    /// <summary>The shared endpoint fans a submission out to every participating engine.</summary>
    public string Endpoint { get; set; } = "https://api.indexnow.org/indexnow";
}

/// <summary>
/// Pushes changed URLs to the IndexNow-participating search engines (Bing, Yandex, Seznam,
/// Naver, and through Bing, DuckDuckGo). Google does not participate, so this speeds up
/// discovery on everything except the engine that matters most — worth having because it is
/// nearly free, not because it replaces earning links.
/// </summary>
public class IndexNowService(
    IHttpClientFactory httpClientFactory,
    ISitemapService sitemap,
    IOptions<IndexNowOptions> options,
    ILogger<IndexNowService> logger) : IIndexNowService
{
    /// <summary>The protocol's per-request ceiling.</summary>
    private const int MaxUrlsPerBatch = 10_000;

    private readonly IndexNowOptions _options = options.Value;

    public bool IsConfigured => _options.Enabled && !string.IsNullOrWhiteSpace(_options.Key);

    public string? KeyLocation =>
        string.IsNullOrWhiteSpace(_options.Key) ? null : $"https://{_options.Host}/{_options.Key}.txt";

    public async Task<IndexNowResult> SubmitAsync(IReadOnlyCollection<string> urls, CancellationToken ct = default)
    {
        if (!IsConfigured)
        {
            return new IndexNowResult(false, 0, 0, "IndexNow is not configured.", []);
        }

        // Submitting a URL on another host earns a 422 for the whole batch, so drop them here
        // rather than losing the good URLs alongside them.
        List<string> ours = urls
            .Where(u => Uri.TryCreate(u, UriKind.Absolute, out Uri? parsed)
                && parsed.Host.Equals(_options.Host, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (ours.Count == 0)
        {
            return new IndexNowResult(false, 0, 0, "No submittable URLs for this host.", []);
        }

        HttpClient client = httpClientFactory.CreateClient();
        List<int> statuses = [];
        int batches = 0;

        for (int offset = 0; offset < ours.Count; offset += MaxUrlsPerBatch)
        {
            List<string> batch = ours.Skip(offset).Take(MaxUrlsPerBatch).ToList();
            batches++;

            try
            {
                HttpResponseMessage response = await client.PostAsJsonAsync(
                    _options.Endpoint,
                    new
                    {
                        host = _options.Host,
                        key = _options.Key,
                        keyLocation = KeyLocation,
                        urlList = batch,
                    },
                    ct);

                statuses.Add((int)response.StatusCode);

                if (!response.IsSuccessStatusCode)
                {
                    logger.LogWarning(
                        "IndexNow rejected a batch of {Count} URLs with {Status}.",
                        batch.Count,
                        (int)response.StatusCode);
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                // A search-engine ping must never take a publish down with it.
                logger.LogWarning(ex, "IndexNow submission failed for a batch of {Count} URLs.", batch.Count);
                statuses.Add(0);
            }
        }

        // 200 accepted, 202 accepted-pending-key-validation. Both mean they took it.
        bool accepted = statuses.Any(s => s is 200 or 202);
        return new IndexNowResult(
            accepted,
            ours.Count,
            batches,
            accepted ? $"Submitted {ours.Count} URLs." : "IndexNow did not accept the submission.",
            statuses);
    }

    public async Task<IndexNowResult> SubmitAllAsync(CancellationToken ct = default)
    {
        IReadOnlyList<string> urls = await sitemap.GetIndexableUrlsAsync();
        return await SubmitAsync(urls, ct);
    }
}
