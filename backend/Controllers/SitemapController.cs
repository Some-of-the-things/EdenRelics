using System.Text;
using Eden_Relics_BE.Data;
using Eden_Relics_BE.Data.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Eden_Relics_BE.Controllers;

[ApiController]
public class SitemapController(EdenRelicsDbContext context) : ControllerBase
{
    private const string BaseUrl = "https://edenrelics.co.uk";

    [HttpGet("api/sitemap.xml")]
    [ResponseCache(Duration = 3600)]
    public async Task<IActionResult> GetSitemap()
    {
        List<Product> products = await context.Products
            .Where(p => !p.IsDeleted)
            .OrderByDescending(p => p.UpdatedAtUtc)
            .ToListAsync();

        List<BlogPost> posts = await context.BlogPosts
            .Where(b => !b.IsDeleted && b.Published)
            .OrderByDescending(b => b.PublishedAtUtc)
            .ToListAsync();

        StringBuilder xml = new();
        xml.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        xml.AppendLine("<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">");

        // Static pages
        string[][] staticPages =
        [
            ["/", "daily", "1.0"],
            ["/contact", "monthly", "0.6"],
            ["/blog", "weekly", "0.7"],
            ["/privacy-policy", "yearly", "0.3"],
            ["/modern-slavery-policy", "yearly", "0.3"],
            ["/supply-chain-policy", "yearly", "0.3"],
            ["/returns-policy", "yearly", "0.3"],
            ["/terms-conditions", "yearly", "0.3"],
            ["/cookie-policy", "yearly", "0.3"],
            ["/accessibility-report", "yearly", "0.3"],
            ["/security", "yearly", "0.3"],
            ["/compliance-report", "yearly", "0.3"],
        ];

        foreach (string[] page in staticPages)
        {
            xml.AppendLine("  <url>");
            xml.AppendLine($"    <loc>{BaseUrl}{page[0]}</loc>");
            xml.AppendLine($"    <changefreq>{page[1]}</changefreq>");
            xml.AppendLine($"    <priority>{page[2]}</priority>");
            xml.AppendLine("  </url>");
        }

        // Product pages
        foreach (Product product in products)
        {
            xml.AppendLine("  <url>");
            xml.AppendLine($"    <loc>{BaseUrl}/product/{product.Id}</loc>");
            xml.AppendLine($"    <lastmod>{product.UpdatedAtUtc:yyyy-MM-dd}</lastmod>");
            xml.AppendLine("    <changefreq>weekly</changefreq>");
            xml.AppendLine("    <priority>0.8</priority>");
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
            xml.AppendLine("  </url>");
        }

        xml.AppendLine("</urlset>");

        return Content(xml.ToString(), "application/xml", Encoding.UTF8);
    }

    private static string Escape(string value) =>
        value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}
