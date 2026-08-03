using System.Text;
using Eden_Relics_BE.Data.Entities;
using Eden_Relics_BE.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Eden_Relics_BE.Services;

public class SitemapService(
    IRepository<Product> products,
    IRepository<BlogPost> posts,
    IRepository<CareFabric> careFabrics,
    IRepository<CareIssue> careIssues,
    SitemapRoutesService staticRoutes) : ISitemapService
{
    private const string BaseUrl = "https://edenrelics.co.uk";

    /// <summary>
    /// One indexable page. Both the sitemap XML and the IndexNow submission are built from these,
    /// so the two can never disagree about which URLs the site is prepared to have crawled.
    /// </summary>
    private sealed record SitemapEntry(
        string Loc,
        string? LastMod,
        string Changefreq,
        string Priority,
        IReadOnlyList<string> Images);

    public async Task<string> BuildSitemapXmlAsync()
    {
        IReadOnlyList<SitemapEntry> entries = await BuildEntriesAsync();

        StringBuilder xml = new();
        xml.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        xml.AppendLine("<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\" xmlns:image=\"http://www.google.com/schemas/sitemap-image/1.1\">");

        foreach (SitemapEntry entry in entries)
        {
            xml.AppendLine("  <url>");
            xml.AppendLine($"    <loc>{entry.Loc}</loc>");
            if (entry.LastMod is not null)
            {
                xml.AppendLine($"    <lastmod>{entry.LastMod}</lastmod>");
            }
            xml.AppendLine($"    <changefreq>{entry.Changefreq}</changefreq>");
            xml.AppendLine($"    <priority>{entry.Priority}</priority>");
            foreach (string image in entry.Images)
            {
                xml.AppendLine("    <image:image>");
                xml.AppendLine($"      <image:loc>{Escape(image)}</image:loc>");
                xml.AppendLine("    </image:image>");
            }
            xml.AppendLine("  </url>");
        }

        xml.AppendLine("</urlset>");
        return xml.ToString();
    }

    public async Task<IReadOnlyList<string>> GetIndexableUrlsAsync()
    {
        IReadOnlyList<SitemapEntry> entries = await BuildEntriesAsync();
        // The XML escapes for markup; a submitted URL must be the real one.
        return entries.Select(e => Unescape(e.Loc)).ToList();
    }

    private async Task<IReadOnlyList<SitemapEntry>> BuildEntriesAsync()
    {
        List<Product> liveProducts = await products.Query()
            .Where(p => p.Status == ProductStatus.Live)
            .OrderByDescending(p => p.UpdatedAtUtc)
            .ToListAsync();

        List<BlogPost> publishedPosts = await posts.Query()
            .Where(b => b.Published)
            .OrderByDescending(b => b.PublishedAtUtc)
            .ToListAsync();

        List<CareFabric> publishedFabrics = await careFabrics.Query()
            .Where(c => c.IsPublished)
            .OrderByDescending(c => c.UpdatedAtUtc)
            .ToListAsync();

        List<CareIssue> publishedIssues = await careIssues.Query()
            .Where(c => c.IsPublished)
            .OrderByDescending(c => c.UpdatedAtUtc)
            .ToListAsync();

        // Static pages — sourced from the frontend's deployed sitemap-routes.json
        // so the sitemap can never advertise URLs the frontend doesn't actually serve.
        IReadOnlyList<SitemapRoute> staticPages = await staticRoutes.GetAsync();

        List<SitemapEntry> entries = [];

        foreach (SitemapRoute page in staticPages)
        {
            entries.Add(new SitemapEntry($"{BaseUrl}{page.Path}", null, page.Changefreq, page.Priority, []));
        }

        foreach (Product product in liveProducts)
        {
            string pathSegment = string.IsNullOrEmpty(product.Slug)
                ? product.Id.ToString()
                : Escape(product.Slug);
            List<string> images = [];
            AddImage(images, product.ImageUrl);
            foreach (string additional in product.AdditionalImageUrls)
            {
                AddImage(images, additional);
            }
            entries.Add(new SitemapEntry(
                $"{BaseUrl}/product/{pathSegment}",
                $"{product.UpdatedAtUtc:yyyy-MM-dd}",
                "weekly",
                "0.8",
                images));
        }

        foreach (BlogPost post in publishedPosts)
        {
            List<string> images = [];
            AddImage(images, post.FeaturedImageUrl);
            entries.Add(new SitemapEntry(
                $"{BaseUrl}/blog/{Escape(post.Slug)}",
                $"{(post.PublishedAtUtc ?? post.UpdatedAtUtc):yyyy-MM-dd}",
                "monthly",
                "0.6",
                images));
        }

        // Vintage care guides (published fabric pages only)
        foreach (CareFabric fabric in publishedFabrics)
        {
            entries.Add(new SitemapEntry(
                $"{BaseUrl}/care/fabric/{Escape(fabric.Slug)}",
                $"{fabric.UpdatedAtUtc:yyyy-MM-dd}",
                "monthly",
                "0.6",
                []));
        }

        // Vintage care guides (published problem pages only)
        foreach (CareIssue issue in publishedIssues)
        {
            entries.Add(new SitemapEntry(
                $"{BaseUrl}/care/problem/{Escape(issue.Slug)}",
                $"{issue.UpdatedAtUtc:yyyy-MM-dd}",
                "monthly",
                "0.6",
                []));
        }

        return entries;
    }

    private static string Escape(string value) =>
        value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    private static string Unescape(string value) =>
        value.Replace("&amp;", "&").Replace("&lt;", "<").Replace("&gt;", ">");

    private static void AddImage(List<string> images, string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }
        images.Add(url);
    }
}
