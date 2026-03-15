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

        // Policies
        ["policy.privacy.updated"] = "March 2026",
        ["policy.privacy.content"] = """
            <section class="policy__section"><h2>1. Introduction</h2><p>DCPNET LTD ("we", "us", "our") is committed to protecting your privacy. This policy explains how we collect, use, and safeguard your personal data when you use our website and services.</p></section>
            <section class="policy__section"><h2>2. Information We Collect</h2><p>We may collect personal information including your name, email address, postal address, phone number, and payment details when you place an order or create an account.</p></section>
            <section class="policy__section"><h2>3. How We Use Your Information</h2><p>We use your information to process orders, provide customer support, send order updates, and improve our services. We will not share your personal data with third parties except as necessary to fulfil orders or comply with legal obligations.</p></section>
            <section class="policy__section"><h2>4. Data Retention</h2><p>We retain your personal data only for as long as necessary to fulfil the purposes for which it was collected, or as required by law.</p></section>
            <section class="policy__section"><h2>5. Your Rights</h2><p>You have the right to access, correct, or delete your personal data. To exercise these rights, please contact us at info&#64;dcpnet.co.uk.</p></section>
            <section class="policy__section"><h2>6. Contact</h2><p>If you have questions about this policy, please contact DCPNET LTD at info&#64;dcpnet.co.uk.</p></section>
            """,

        ["policy.returns.updated"] = "March 2026",
        ["policy.returns.content"] = """
            <section class="policy__section"><h2>1. Overview</h2><p>At Eden Relics, every item we sell is a unique vintage piece. We want you to be delighted with your purchase, and we understand that buying vintage online requires trust. This policy sets out your rights and our process for returns and exchanges.</p></section>
            <section class="policy__section"><h2>2. Your Right to Cancel</h2><p>Under the Consumer Contracts Regulations 2013, you have the right to cancel your order within 14 days of receiving your item, without giving any reason. To exercise this right, please contact us at edenrelics&#64;dcp-net.com stating your order number and that you wish to cancel. You then have a further 14 days to return the item to us.</p></section>
            <section class="policy__section"><h2>3. Conditions for Returns</h2><p>Items must be returned in the same condition in which they were received. They must be unworn (other than to try on), unwashed, and with any tags still attached. As our items are vintage and one-of-a-kind, we ask that you handle them with particular care. We reserve the right to refuse a return or apply a deduction if an item is returned in a condition that is materially different from when it was dispatched.</p></section>
            <section class="policy__section"><h2>4. How to Return an Item</h2><p>To initiate a return, please email us at edenrelics&#64;dcp-net.com with your order number. We will provide you with the return address and any further instructions. Please package the item securely to prevent damage in transit. We recommend using a tracked delivery service, as we cannot be held responsible for items lost in the post.</p></section>
            <section class="policy__section"><h2>5. Return Postage Costs</h2><p>If you are returning an item because you have changed your mind, you are responsible for the cost of return postage. If an item is faulty or not as described, we will cover the return postage costs. Please contact us before returning a faulty item so that we can arrange this.</p></section>
            <section class="policy__section"><h2>6. Refunds</h2><p>Once we have received and inspected your return, we will process your refund within 5 working days. Refunds will be issued to the original payment method. Please allow up to 10 working days for the refund to appear in your account, depending on your payment provider.</p></section>
            <section class="policy__section"><h2>7. Exchanges</h2><p>As our items are unique vintage pieces, we are unable to offer direct exchanges. If you would like a different item, please return your original purchase for a refund and place a new order. We cannot guarantee that other items will still be available, as all stock is one-of-a-kind.</p></section>
            <section class="policy__section"><h2>8. Faulty or Incorrectly Described Items</h2><p>We take great care to accurately describe and photograph every item. If you receive an item that is faulty or significantly not as described, please contact us within 30 days of receipt. We will offer a full refund including original and return postage costs. Please note that minor signs of wear are expected with vintage garments and will be noted in the item description and condition grading.</p></section>
            <section class="policy__section"><h2>9. Sale Items</h2><p>Items purchased in a sale or at a reduced price are subject to the same returns policy as full-price items. Your statutory rights are not affected.</p></section>
            <section class="policy__section"><h2>10. Contact Us</h2><p>If you have any questions about our returns policy, please contact us at edenrelics&#64;dcp-net.com or by post at DCPNET LTD, 30 Vane Close, Norwich, NR7 0US, United Kingdom.</p></section>
            """,

        ["policy.supply-chain.updated"] = "March 2026",
        ["policy.supply-chain.content"] = """
            <section class="policy__section"><h2>1. Introduction</h2><p>DCPNET LTD is committed to responsible and ethical sourcing across our entire supply chain. This policy outlines our standards and expectations for all suppliers and partners.</p></section>
            <section class="policy__section"><h2>2. Ethical Sourcing</h2><p>We source vintage garments through trusted networks of dealers, estate sales, and verified second-hand suppliers. We prioritise transparency and traceability in all sourcing activities.</p></section>
            <section class="policy__section"><h2>3. Environmental Responsibility</h2><p>By specialising in vintage and pre-owned clothing, we actively contribute to reducing textile waste and the environmental impact of the fashion industry. We encourage sustainable practices throughout our supply chain.</p></section>
            <section class="policy__section"><h2>4. Supplier Standards</h2><p>All suppliers are expected to comply with applicable laws and regulations, maintain fair labour practices, and uphold standards consistent with our values of integrity and respect.</p></section>
            <section class="policy__section"><h2>5. Monitoring &amp; Review</h2><p>We regularly review our supply chain practices and supplier relationships to ensure ongoing compliance with this policy. Concerns can be raised at info&#64;dcpnet.co.uk.</p></section>
            """,

        ["policy.modern-slavery.updated"] = "March 2026",
        ["policy.modern-slavery.content"] = """
            <section class="policy__section"><h2>1. Introduction</h2><p>DCPNET LTD is committed to preventing modern slavery and human trafficking in all aspects of our business and supply chain. This statement is made pursuant to Section 54 of the Modern Slavery Act 2015.</p></section>
            <section class="policy__section"><h2>2. Our Business</h2><p>DCPNET LTD operates as a vintage clothing retailer, sourcing carefully curated garments from a variety of suppliers and sellers across multiple regions.</p></section>
            <section class="policy__section"><h2>3. Our Commitment</h2><p>We are committed to ensuring that there is no modern slavery or human trafficking in our supply chains or any part of our business. We expect the same standards from our suppliers and partners.</p></section>
            <section class="policy__section"><h2>4. Due Diligence</h2><p>We conduct due diligence on all new suppliers and regularly review existing suppliers. We assess and manage the risk of modern slavery occurring in our supply chains.</p></section>
            <section class="policy__section"><h2>5. Reporting Concerns</h2><p>We encourage anyone with concerns about modern slavery in any part of our business or supply chain to contact us at info&#64;dcpnet.co.uk.</p></section>
            """,
    };
}
