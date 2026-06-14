namespace Eden_Relics_BE.Services;

public interface ISitemapService
{
    /// <summary>Builds the sitemap.xml body (static routes + live products + published posts).</summary>
    Task<string> BuildSitemapXmlAsync();
}
