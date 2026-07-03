using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Eden_Relics_BE.Data.Entities;
using Eden_Relics_BE.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Eden_Relics_BE.Services;

/// <summary>
/// Generates a live Google Merchant Center product feed (RSS 2.0 with the
/// http://base.google.com/ns/1.0 namespace) from currently-live products.
/// Served fresh so one-of-one availability never goes stale — a sold piece
/// drops out of the feed on the next fetch rather than being flagged as an
/// availability mismatch.
/// </summary>
public partial class MerchantFeedService(IRepository<Product> products) : IMerchantFeedService
{
    private const string BaseUrl = "https://edenrelics.co.uk";

    // One shipping rate per destination country, sourced from ShippingZones so
    // the feed can never advertise a country (or price) checkout won't honour.
    // Deduped by country code, keeping the first (standard) rate — e.g. GB maps
    // to free standard UK shipping, not the £6.95 express rate.
    private static readonly IReadOnlyList<(string Country, decimal Price)> ShippingRates = BuildShippingRates();

    private static List<(string Country, decimal Price)> BuildShippingRates()
    {
        List<(string, decimal)> rates = [];
        HashSet<string> seen = [];
        foreach (ShippingZone zone in ShippingZones.All)
        {
            foreach (ShippingCountry country in zone.Countries)
            {
                if (seen.Add(country.Code))
                {
                    rates.Add((country.Code, zone.Price));
                }
            }
        }
        return rates;
    }

    // Google Shopping requires a real colour word for apparel. Ordered longest-first
    // so multi-word tells ("forest green") win over their single-word substring.
    private static readonly string[] ColourWords =
    [
        "powder blue", "forest green", "navy blue", "off white", "dusky pink",
        "black", "white", "cream", "ivory", "navy", "blue", "teal", "green",
        "olive", "sage", "khaki", "burgundy", "maroon", "red", "coral", "pink",
        "purple", "lilac", "lavender", "plum", "yellow", "mustard", "orange",
        "brown", "chocolate", "tan", "beige", "camel", "grey", "gray", "gold",
        "silver", "turquoise", "peach", "rust", "wine",
    ];

    public async Task<string> BuildFeedXmlAsync()
    {
        List<Product> liveProducts = await products.Query()
            .Where(p => p.Status == ProductStatus.Live)
            .OrderByDescending(p => p.UpdatedAtUtc)
            .ToListAsync();

        StringBuilder xml = new();
        xml.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        xml.AppendLine("<rss version=\"2.0\" xmlns:g=\"http://base.google.com/ns/1.0\">");
        xml.AppendLine("  <channel>");
        xml.AppendLine("    <title>Eden Relics</title>");
        xml.AppendLine($"    <link>{BaseUrl}</link>");
        xml.AppendLine("    <description>Curated one-of-one vintage dresses.</description>");

        foreach (Product product in liveProducts)
        {
            AppendItem(xml, product);
        }

        xml.AppendLine("  </channel>");
        xml.AppendLine("</rss>");
        return xml.ToString();
    }

    private static void AppendItem(StringBuilder xml, Product product)
    {
        string pathSegment = string.IsNullOrEmpty(product.Slug)
            ? product.Id.ToString()
            : Escape(product.Slug);
        string offerId = string.IsNullOrWhiteSpace(product.Sku) ? product.Id.ToString() : product.Sku;
        bool onSale = product is { SalePrice: > 0 } && product.SalePrice < product.Price;

        xml.AppendLine("    <item>");
        xml.AppendLine($"      <g:id>{Escape(offerId)}</g:id>");
        xml.AppendLine($"      <g:title>{Escape(product.Name)}</g:title>");
        xml.AppendLine($"      <g:description><![CDATA[{PlainText(product.Description)}]]></g:description>");
        xml.AppendLine($"      <g:link>{BaseUrl}/product/{pathSegment}</g:link>");
        xml.AppendLine($"      <g:image_link>{Escape(product.ImageUrl)}</g:image_link>");

        // Google accepts up to 10 additional images.
        foreach (string extra in product.AdditionalImageUrls.Where(u => !string.IsNullOrWhiteSpace(u)).Take(10))
        {
            xml.AppendLine($"      <g:additional_image_link>{Escape(extra)}</g:additional_image_link>");
        }

        xml.AppendLine("      <g:availability>in_stock</g:availability>");
        xml.AppendLine($"      <g:price>{Money(product.Price)}</g:price>");
        if (onSale)
        {
            xml.AppendLine($"      <g:sale_price>{Money(product.SalePrice!.Value)}</g:sale_price>");
        }

        // Every item is genuine vintage, so used condition with no manufacturer
        // barcode. Declaring identifier_exists=no makes brand/GTIN optional.
        xml.AppendLine("      <g:condition>used</g:condition>");
        xml.AppendLine("      <g:identifier_exists>no</g:identifier_exists>");
        xml.AppendLine("      <g:google_product_category>Apparel &amp; Accessories &gt; Clothing &gt; Dresses</g:google_product_category>");
        xml.AppendLine($"      <g:product_type>{Escape(product.Era)}</g:product_type>");

        // Apparel attributes Google expects for GB Shopping.
        xml.AppendLine("      <g:gender>female</g:gender>");
        xml.AppendLine("      <g:age_group>adult</g:age_group>");
        if (!string.IsNullOrWhiteSpace(product.Size))
        {
            xml.AppendLine($"      <g:size>{Escape(product.Size)}</g:size>");
        }

        string? colour = DetectColour(product.Name);
        if (colour is not null)
        {
            xml.AppendLine($"      <g:color>{Escape(colour)}</g:color>");
        }

        if (!string.IsNullOrWhiteSpace(product.Material))
        {
            xml.AppendLine($"      <g:material>{Escape(product.Material)}</g:material>");
        }

        foreach ((string country, decimal price) in ShippingRates)
        {
            xml.AppendLine("      <g:shipping>");
            xml.AppendLine($"        <g:country>{country}</g:country>");
            xml.AppendLine($"        <g:price>{Money(price)}</g:price>");
            xml.AppendLine("      </g:shipping>");
        }
        xml.AppendLine("    </item>");
    }

    /// <summary>Picks the first recognisable colour word from the product name, or null.</summary>
    private static string? DetectColour(string name)
    {
        string lower = name.ToLowerInvariant();
        foreach (string colour in ColourWords)
        {
            if (lower.Contains(colour, StringComparison.Ordinal))
            {
                return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(colour);
            }
        }
        return null;
    }

    private static string Money(decimal amount) =>
        amount.ToString("0.00", CultureInfo.InvariantCulture) + " GBP";

    /// <summary>Strips HTML tags/entities from a description down to feed-safe plain text.</summary>
    private static string PlainText(string html)
    {
        string withBreaks = html.Replace("<br>", " ").Replace("<br/>", " ").Replace("<br />", " ");
        string stripped = TagPattern().Replace(withBreaks, string.Empty);
        string decoded = System.Net.WebUtility.HtmlDecode(stripped);
        string collapsed = WhitespacePattern().Replace(decoded, " ").Trim();
        // CDATA can't contain the literal terminator.
        return collapsed.Replace("]]>", "]] >");
    }

    private static string Escape(string value) =>
        value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex TagPattern();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespacePattern();
}
