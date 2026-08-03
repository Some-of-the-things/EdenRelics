namespace Eden_Relics_BE.Services;

/// <summary>The outcome of one submission attempt, safe to return to the admin UI.</summary>
public record IndexNowResult(
    bool Submitted,
    int UrlCount,
    int BatchCount,
    string Message,
    IReadOnlyList<int> StatusCodes);

/// <summary>
/// Submits changed URLs to the IndexNow search engines.
///
/// Implementations MUST NOT throw. Publish paths await these calls inline, so an exception here
/// fails an editor's save because a search engine was unreachable. Failures are reported through
/// <see cref="IndexNowResult"/> and the log, never as exceptions.
/// </summary>
public interface IIndexNowService
{
    /// <summary>True when a key is configured and submission is switched on.</summary>
    bool IsConfigured { get; }

    /// <summary>Where the ownership key file must be reachable. Null when unconfigured.</summary>
    string? KeyLocation { get; }

    /// <summary>
    /// Submits specific URLs. Silently a no-op when unconfigured, so publish paths can call this
    /// unconditionally without a search-engine ping ever being able to fail a content change.
    /// </summary>
    Task<IndexNowResult> SubmitAsync(IReadOnlyCollection<string> urls, CancellationToken ct = default);

    /// <summary>Submits every URL the sitemap advertises — the one-shot "here is the whole site".</summary>
    Task<IndexNowResult> SubmitAllAsync(CancellationToken ct = default);

    /// <summary>
    /// Submits site-relative paths ("/blog/some-post"), resolved against the configured host. This
    /// is what publish paths call, so no caller needs its own copy of the base URL.
    /// </summary>
    Task<IndexNowResult> SubmitPathsAsync(IReadOnlyCollection<string> paths, CancellationToken ct = default);
}
