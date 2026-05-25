using System.Text;
using Eden_Relics_BE.Data;
using Eden_Relics_BE.Data.Entities;
using Eden_Relics_BE.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Eden_Relics_BE.Controllers;

[ApiController]
public class SitemapController(
    EdenRelicsDbContext context,
    SitemapRoutesService staticRoutes) : ControllerBase
{
    private const string BaseUrl = "https://edenrelics.co.uk";

    [HttpGet("api/sitemap.xml")]
    [ResponseCache(Duration = 3600)]
    public async Task<IActionResult> GetSitemap()
    {
        List<Product> products = await context.Products
            .Where(p => !p.IsDeleted && p.Status == ProductStatus.Live)
            .OrderByDescending(p => p.UpdatedAtUtc)
            .ToListAsync();

        List<BlogPost> posts = await context.BlogPosts
            .Where(b => !b.IsDeleted && b.Published)
            .OrderByDescending(b => b.PublishedAtUtc)
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
        foreach (Product product in products)
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
        foreach (BlogPost post in posts)
        {
            xml.AppendLine("  <url>");
            xml.AppendLine($"    <loc>{BaseUrl}/blog/{Escape(post.Slug)}</loc>");
            xml.AppendLine($"    <lastmod>{(post.PublishedAtUtc ?? post.UpdatedAtUtc):yyyy-MM-dd}</lastmod>");
            xml.AppendLine("    <changefreq>monthly</changefreq>");
            xml.AppendLine("    <priority>0.6</priority>");
            AppendImage(xml, post.FeaturedImageUrl);
            xml.AppendLine("  </url>");
        }

        xml.AppendLine("</urlset>");

        return Content(xml.ToString(), "application/xml", Encoding.UTF8);
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
