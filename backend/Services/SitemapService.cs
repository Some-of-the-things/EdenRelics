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

    public async Task<string> BuildSitemapXmlAsync()
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

        StringBuilder xml = new();
        xml.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        xml.AppendLine("<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\" xmlns:image=\"http://www.google.com/schemas/sitemap-image/1.1\">");

        foreach (SitemapRoute page in staticPages)
        {
            xml.AppendLine("  <url>");
            xml.AppendLine($"    <loc>{BaseUrl}{page.Path}</loc>");
            xml.AppendLine($"    <changefreq>{page.Changefreq}</changefreq>");
            xml.AppendLine($"    <priority>{page.Priority}</priority>");
            xml.AppendLine("  </url>");
        }

        // Product pages
        foreach (Product product in liveProducts)
        {
            string pathSegment = string.IsNullOrEmpty(product.Slug)
                ? product.Id.ToString()
                : Escape(product.Slug);
            xml.AppendLine("  <url>");
            xml.AppendLine($"    <loc>{BaseUrl}/product/{pathSegment}</loc>");
            xml.AppendLine($"    <lastmod>{product.UpdatedAtUtc:yyyy-MM-dd}</lastmod>");
            xml.AppendLine("    <changefreq>weekly</changefreq>");
            xml.AppendLine("    <priority>0.8</priority>");
            AppendImage(xml, product.ImageUrl);
            foreach (string additional in product.AdditionalImageUrls)
            {
                AppendImage(xml, additional);
            }
            xml.AppendLine("  </url>");
        }

        // Blog posts
        foreach (BlogPost post in publishedPosts)
        {
            xml.AppendLine("  <url>");
            xml.AppendLine($"    <loc>{BaseUrl}/blog/{Escape(post.Slug)}</loc>");
            xml.AppendLine($"    <lastmod>{(post.PublishedAtUtc ?? post.UpdatedAtUtc):yyyy-MM-dd}</lastmod>");
            xml.AppendLine("    <changefreq>monthly</changefreq>");
            xml.AppendLine("    <priority>0.6</priority>");
            AppendImage(xml, post.FeaturedImageUrl);
            xml.AppendLine("  </url>");
        }

        // Vintage care guides (published fabric pages only)
        foreach (CareFabric fabric in publishedFabrics)
        {
            xml.AppendLine("  <url>");
            xml.AppendLine($"    <loc>{BaseUrl}/care/fabric/{Escape(fabric.Slug)}</loc>");
            xml.AppendLine($"    <lastmod>{fabric.UpdatedAtUtc:yyyy-MM-dd}</lastmod>");
            xml.AppendLine("    <changefreq>monthly</changefreq>");
            xml.AppendLine("    <priority>0.6</priority>");
            xml.AppendLine("  </url>");
        }

        // Vintage care guides (published problem pages only)
        foreach (CareIssue issue in publishedIssues)
        {
            xml.AppendLine("  <url>");
            xml.AppendLine($"    <loc>{BaseUrl}/care/problem/{Escape(issue.Slug)}</loc>");
            xml.AppendLine($"    <lastmod>{issue.UpdatedAtUtc:yyyy-MM-dd}</lastmod>");
            xml.AppendLine("    <changefreq>monthly</changefreq>");
            xml.AppendLine("    <priority>0.6</priority>");
            xml.AppendLine("  </url>");
        }

        xml.AppendLine("</urlset>");
        return xml.ToString();
    }

    private static string Escape(string value) =>
        value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    private static void AppendImage(StringBuilder xml, string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }
        xml.AppendLine("    <image:image>");
        xml.AppendLine($"      <image:loc>{Escape(url)}</image:loc>");
        xml.AppendLine("    </image:image>");
    }
}
