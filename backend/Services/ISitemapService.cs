namespace Eden_Relics_BE.Services;

public interface ISitemapService
{
    /// <summary>Builds the sitemap.xml body (static routes + live products + published posts).</summary>
    Task<string> BuildSitemapXmlAsync();

    /// <summary>
    /// The same URLs the sitemap advertises, unescaped and ready to submit to a search engine.
    /// Shares its source with <see cref="BuildSitemapXmlAsync"/> so the two cannot drift.
    /// </summary>
    Task<IReadOnlyList<string>> GetIndexableUrlsAsync();
}
