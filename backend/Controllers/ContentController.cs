using Eden_Relics_BE.Data;
using Eden_Relics_BE.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Eden_Relics_BE.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ContentController(EdenRelicsDbContext context) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<Dictionary<string, string>>> GetAll()
    {
        List<SiteContent> entries = await context.SiteContent.ToListAsync();
        Dictionary<string, string> result = entries.ToDictionary(e => e.Key, e => e.Value);

        // Merge defaults for any missing keys
        foreach (KeyValuePair<string, string> kv in Defaults)
        {
            result.TryAdd(kv.Key, kv.Value);
        }

        return Ok(result);
    }

    [HttpPut]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<Dictionary<string, string>>> UpdateAll([FromBody] Dictionary<string, string> content)
    {
        List<SiteContent> existing = await context.SiteContent.ToListAsync();
        Dictionary<string, SiteContent> byKey = existing.ToDictionary(e => e.Key);

        foreach (KeyValuePair<string, string> kv in content)
        {
            if (byKey.TryGetValue(kv.Key, out SiteContent? entry))
            {
                entry.Value = kv.Value;
            }
            else
            {
                context.SiteContent.Add(new SiteContent { Key = kv.Key, Value = kv.Value });
            }
        }

        await context.SaveChangesAsync();

        // Return full content including defaults
        List<SiteContent> updated = await context.SiteContent.ToListAsync();
        Dictionary<string, string> result = updated.ToDictionary(e => e.Key, e => e.Value);
        foreach (KeyValuePair<string, string> kv in Defaults)
        {
            result.TryAdd(kv.Key, kv.Value);
        }

        return Ok(result);
    }

    private static readonly Dictionary<string, string> Defaults = new()
    {
        // Hero
        ["home.hero.eyebrow"] = "Carefully Sourced & Lovingly Preserved",
        ["home.hero.title"] = "Curated Vintage",
        ["home.hero.subtitle"] = "Timeless pieces from decades past.",

        // About
        ["home.about.title"] = "Why Eden Relics?",
        ["home.about.card1.title"] = "Authentically Vintage",
        ["home.about.card1.text"] = "Every piece in our collection is a genuine vintage garment, carefully sourced from estate sales, vintage fairs and private collections across the UK and Europe. We never sell reproductions.",
        ["home.about.card2.title"] = "Quality Assured",
        ["home.about.card2.text"] = "Each dress is inspected, gently cleaned and assessed for condition before it reaches our shop. We grade every item honestly so you know exactly what you are getting, from mint condition to well-loved pieces with character.",
        ["home.about.card3.title"] = "Sustainable Fashion",
        ["home.about.card3.text"] = "Choosing vintage is one of the most sustainable ways to build your wardrobe. By giving these beautiful garments a second life, you are reducing textile waste and embracing slow fashion without compromising on style.",
        ["home.about.card4.title"] = "Spanning the Decades",
        ["home.about.card4.text"] = "From flowing 1970s bohemian maxis and bold 1980s power dresses to minimalist 1990s slip dresses, nostalgic Y2K styles and contemporary pieces, our collection celebrates the best fashion from every era.",

        // Footer
        ["footer.tagline"] = "Carefully sourced & lovingly preserved vintage clothing.",
        ["footer.company.line1"] = "Company No. 15734157",
        ["footer.company.line2"] = "VAT Reg. No. 512 3682 13",
        ["footer.company.line3"] = "Eden Relics is a trading name of DCPNET LTD.",
        ["footer.contact.email"] = "edenrelics@dcp-net.com",
        ["footer.contact.phone"] = "+44 (0) 7454 705183",
        ["footer.contact.address"] = "DCPNET LTD\n30 Vane Close\nNorwich, NR7 0US\nUnited Kingdom",

        // Contact page
        ["contact.title"] = "Get in Touch",
        ["contact.subtitle"] = "Have a question or want to know more? Drop us a message.",
    };
}
