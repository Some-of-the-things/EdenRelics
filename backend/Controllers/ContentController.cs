using Eden_Relics_BE.Data;
using Eden_Relics_BE.Data.Entities;
using Eden_Relics_BE.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Eden_Relics_BE.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ContentController(EdenRelicsDbContext context) : ControllerBase
{
    private static readonly HashSet<string> LocalePrefixes = ["en", "fr", "de", "es", "it", "nl", "pt", "sv", "da", "nb", "ja", "ko"];
    [HttpGet]
    public async Task<ActionResult<Dictionary<string, string>>> GetAll([FromQuery] string? locale = null)
    {
        List<SiteContent> entries = await context.SiteContent.ToListAsync();
        Dictionary<string, string> all = entries.ToDictionary(e => e.Key, e => e.Value);

        // Merge defaults for any missing keys
        foreach (KeyValuePair<string, string> kv in Defaults)
        {
            all.TryAdd(kv.Key, kv.Value);
        }

        // If a locale is requested, return translated content where available
        if (!string.IsNullOrWhiteSpace(locale) && locale != "en")
        {
            string prefix = $"{locale}.";
            Dictionary<string, string> result = [];

            foreach (KeyValuePair<string, string> kv in all)
            {
                if (IsLocalePrefixedKey(kv.Key))
                {
                    continue;
                }

                string localisedKey = prefix + kv.Key;
                result[kv.Key] = all.GetValueOrDefault(localisedKey, kv.Value);
            }

            return Ok(result);
        }

        // Default: return English content, stripping locale-prefixed keys
        Dictionary<string, string> english = [];
        foreach (KeyValuePair<string, string> kv in all)
        {
            if (!IsLocalePrefixedKey(kv.Key))
            {
                english[kv.Key] = kv.Value;
            }
        }

        return Ok(english);
    }

    /// <summary>Check if a key is prefixed with a supported locale code (e.g., "fr.home.hero.title").</summary>
    private static bool IsLocalePrefixedKey(string key)
    {
        return key.Length > 3 && key[2] == '.' && LocalePrefixes.Contains(key[..2]);
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

        TranslationBackgroundService.EnqueueContent(content);

        // Return full English content including defaults
        List<SiteContent> updated = await context.SiteContent.ToListAsync();
        Dictionary<string, string> result = [];
        foreach (SiteContent e in updated)
        {
            if (!IsLocalePrefixedKey(e.Key))
            {
                result[e.Key] = e.Value;
            }
        }
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

        // Header announcement banner (set to empty string to hide)
        ["header.banner"] = "Free shipping on UK orders",

        // Footer
        ["footer.tagline"] = "Carefully sourced & lovingly preserved vintage clothing.",
        ["footer.company.line1"] = "Company No. 17153907",
        ["footer.company.line2"] = "Registered in England and Wales",
        ["footer.company.line3"] = "",
        ["footer.contact.email"] = "edenrelics@dcp-net.com",
        ["footer.contact.phone"] = "+44 (0) 7454 705183",
        ["footer.contact.address"] = "EDEN RELICS LTD\n30 Vane Close\nNorwich, NR7 0US\nUnited Kingdom",

        // Contact page
        ["contact.title"] = "Get in Touch",
        ["contact.subtitle"] = "Have a question or want to know more? Drop us a message.",

        // Policies
        ["policy.privacy.updated"] = "April 2026",
        ["policy.privacy.content"] = """
            <div class="policy__section"><h2>1. Introduction</h2><p>EDEN RELICS LTD ("we", "us", "our") is committed to protecting your privacy. This policy explains how we collect, use, and safeguard your personal data when you use our website and services, in accordance with the UK General Data Protection Regulation (UK GDPR) and the Data Protection Act 2018.</p></div>
            <div class="policy__section"><h2>2. Data Controller</h2><p>The data controller is EDEN RELICS LTD, Company No. 17153907, registered at 30 Vane Close, Norwich, NR7 0US, United Kingdom. For data protection enquiries, contact us at edenrelics&#64;dcp-net.com.</p></div>
            <div class="policy__section"><h2>3. Information We Collect</h2><p>We may collect personal information including your name, email address, postal address, phone number, and payment card details (last four digits only) when you place an order, create an account, subscribe to our mailing list, or contact us. We also collect technical data such as your IP address and browser type when you use our website.</p></div>
            <div class="policy__section"><h2>4. Lawful Basis for Processing</h2><p>We process your personal data on the following legal bases: <strong>Contract</strong> &ndash; to fulfil orders and provide our services; <strong>Legal obligation</strong> &ndash; to comply with tax, accounting, and other legal requirements; <strong>Legitimate interests</strong> &ndash; to improve our services and prevent fraud; <strong>Consent</strong> &ndash; for marketing communications and non-essential cookies, which you can withdraw at any time.</p></div>
            <div class="policy__section"><h2>5. How We Use Your Information</h2><p>We use your information to process orders and payments, provide customer support, send order updates and dispatch notifications, improve our services and website experience, and comply with legal obligations.</p></div>
            <div class="policy__section"><h2>6. Third-Party Data Processors</h2><p>We share your personal data with the following third-party processors, solely for the purposes stated: <strong>Stripe</strong> (payment processing &ndash; your card details are handled directly by Stripe and never stored on our servers); <strong>Resend</strong> (transactional email delivery &ndash; order confirmations, dispatch notifications); <strong>Google, Facebook, Apple</strong> (social login authentication, only if you choose to sign in via these providers). All processors are contractually obligated to handle your data in accordance with UK GDPR.</p></div>
            <div class="policy__section"><h2>7. Cookies</h2><p>We use essential cookies to operate our website and, with your consent, non-essential cookies for social login features. For full details, please see our <a href="/cookie-policy">Cookie Policy</a>.</p></div>
            <div class="policy__section"><h2>8. Data Retention</h2><p>We retain your personal data only for as long as necessary to fulfil the purposes for which it was collected. Order data is retained for 6 years to comply with HMRC requirements. Account data is retained until you request deletion. Mailing list data is retained until you unsubscribe.</p></div>
            <div class="policy__section"><h2>9. Your Rights</h2><p>Under UK GDPR, you have the right to: <strong>Access</strong> your personal data; <strong>Rectify</strong> inaccurate data; <strong>Erase</strong> your data (right to be forgotten); <strong>Restrict</strong> processing; <strong>Data portability</strong> &ndash; receive your data in a structured format; <strong>Object</strong> to processing based on legitimate interests; <strong>Withdraw consent</strong> at any time for consent-based processing. You can exercise the right to access and delete your data from your account settings page. For all other requests, contact us at edenrelics&#64;dcp-net.com. We will respond within one month.</p></div>
            <div class="policy__section"><h2>10. Data Breach Notification</h2><p>In the event of a personal data breach that poses a risk to your rights and freedoms, we will notify the Information Commissioner's Office (ICO) within 72 hours and inform affected individuals without undue delay.</p></div>
            <div class="policy__section"><h2>11. Complaints</h2><p>If you are unhappy with how we handle your data, you have the right to lodge a complaint with the ICO at ico.org.uk or by calling 0303 123 1113.</p></div>
            <div class="policy__section"><h2>12. Contact</h2><p>For data protection enquiries, contact EDEN RELICS LTD at edenrelics&#64;dcp-net.com or by post at 30 Vane Close, Norwich, NR7 0US, United Kingdom.</p></div>
            """,

        ["policy.returns.updated"] = "March 2026",
        ["policy.returns.content"] = """
            <div class="policy__section"><h2>1. Overview</h2><p>At Eden Relics, every item we sell is a unique vintage piece. We want you to be delighted with your purchase, and we understand that buying vintage online requires trust. This policy sets out your rights and our process for returns and exchanges.</p></div>
            <div class="policy__section"><h2>2. Your Right to Cancel</h2><p>Under the Consumer Contracts Regulations 2013, you have the right to cancel your order within 14 days of receiving your item, without giving any reason. To exercise this right, please contact us at edenrelics&#64;dcp-net.com stating your order number and that you wish to cancel. You then have a further 14 days to return the item to us.</p></div>
            <div class="policy__section"><h2>3. Conditions for Returns</h2><p>Items must be returned in the same condition in which they were received. They must be unworn (other than to try on), unwashed, and with any tags still attached. As our items are vintage and one-of-a-kind, we ask that you handle them with particular care. We reserve the right to refuse a return or apply a deduction if an item is returned in a condition that is materially different from when it was dispatched.</p></div>
            <div class="policy__section"><h2>4. How to Return an Item</h2><p>To initiate a return, please email us at edenrelics&#64;dcp-net.com with your order number. We will provide you with the return address and any further instructions. Please package the item securely to prevent damage in transit. We recommend using a tracked delivery service, as we cannot be held responsible for items lost in the post.</p></div>
            <div class="policy__section"><h2>5. Return Postage Costs</h2><p>If you are returning an item because you have changed your mind, you are responsible for the cost of return postage. If an item is faulty or not as described, we will cover the return postage costs. Please contact us before returning a faulty item so that we can arrange this.</p></div>
            <div class="policy__section"><h2>6. Refunds</h2><p>Once we have received and inspected your return, we will process your refund within 5 working days. Refunds will be issued to the original payment method. Please allow up to 10 working days for the refund to appear in your account, depending on your payment provider.</p></div>
            <div class="policy__section"><h2>7. Exchanges</h2><p>As our items are unique vintage pieces, we are unable to offer direct exchanges. If you would like a different item, please return your original purchase for a refund and place a new order. We cannot guarantee that other items will still be available, as all stock is one-of-a-kind.</p></div>
            <div class="policy__section"><h2>8. Faulty or Incorrectly Described Items</h2><p>We take great care to accurately describe and photograph every item. If you receive an item that is faulty or significantly not as described, please contact us within 30 days of receipt. We will offer a full refund including original and return postage costs. Please note that minor signs of wear are expected with vintage garments and will be noted in the item description and condition grading.</p></div>
            <div class="policy__section"><h2>9. Sale Items</h2><p>Items purchased in a sale or at a reduced price are subject to the same returns policy as full-price items. Your statutory rights are not affected.</p></div>
            <div class="policy__section"><h2>10. Contact Us</h2><p>If you have any questions about our returns policy, please contact us at edenrelics&#64;dcp-net.com or by post at EDEN RELICS LTD, 30 Vane Close, Norwich, NR7 0US, United Kingdom.</p></div>
            """,

        ["policy.supply-chain.updated"] = "March 2026",
        ["policy.supply-chain.content"] = """
            <div class="policy__section"><h2>1. Introduction</h2><p>EDEN RELICS LTD is committed to responsible and ethical sourcing across our entire supply chain. This policy outlines our standards and expectations for all suppliers and partners.</p></div>
            <div class="policy__section"><h2>2. Ethical Sourcing</h2><p>We source vintage garments through trusted networks of dealers, estate sales, and verified second-hand suppliers. We prioritise transparency and traceability in all sourcing activities.</p></div>
            <div class="policy__section"><h2>3. Environmental Responsibility</h2><p>By specialising in vintage and pre-owned clothing, we actively contribute to reducing textile waste and the environmental impact of the fashion industry. We encourage sustainable practices throughout our supply chain.</p></div>
            <div class="policy__section"><h2>4. Supplier Standards</h2><p>All suppliers are expected to comply with applicable laws and regulations, maintain fair labour practices, and uphold standards consistent with our values of integrity and respect.</p></div>
            <div class="policy__section"><h2>5. Monitoring &amp; Review</h2><p>We regularly review our supply chain practices and supplier relationships to ensure ongoing compliance with this policy. Concerns can be raised at info&#64;dcpnet.co.uk.</p></div>
            """,

        ["policy.modern-slavery.updated"] = "March 2026",
        ["policy.modern-slavery.content"] = """
            <div class="policy__section"><h2>1. Introduction</h2><p>EDEN RELICS LTD is committed to preventing modern slavery and human trafficking in all aspects of our business and supply chain. This statement is made pursuant to Section 54 of the Modern Slavery Act 2015.</p></div>
            <div class="policy__section"><h2>2. Our Business</h2><p>EDEN RELICS LTD operates as a vintage clothing retailer, sourcing carefully curated garments from a variety of suppliers and sellers across multiple regions.</p></div>
            <div class="policy__section"><h2>3. Our Commitment</h2><p>We are committed to ensuring that there is no modern slavery or human trafficking in our supply chains or any part of our business. We expect the same standards from our suppliers and partners.</p></div>
            <div class="policy__section"><h2>4. Due Diligence</h2><p>We conduct due diligence on all new suppliers and regularly review existing suppliers. We assess and manage the risk of modern slavery occurring in our supply chains.</p></div>
            <div class="policy__section"><h2>5. Reporting Concerns</h2><p>We encourage anyone with concerns about modern slavery in any part of our business or supply chain to contact us at info&#64;dcpnet.co.uk.</p></div>
            """,

        ["policy.terms.updated"] = "April 2026",
        ["policy.terms.content"] = """
            <div class="policy__section"><h2>1. Introduction</h2><p>These Terms and Conditions ("Terms") govern your use of the Eden Relics website (edenrelics.co.uk) operated by EDEN RELICS LTD (Company No. 17153907), registered at 30 Vane Close, Norwich, NR7 0US, United Kingdom ("we", "us", "our"). By using our website or placing an order, you agree to be bound by these Terms.</p></div>
            <div class="policy__section"><h2>2. Products &amp; Pricing</h2><p>All items sold through Eden Relics are vintage, pre-owned garments. Each item is unique and one-of-a-kind. We take great care to accurately describe and photograph every item, including noting any signs of wear or imperfections. All prices displayed on the website are in pounds sterling (GBP). We reserve the right to amend prices at any time, but changes will not affect orders already confirmed.</p></div>
            <div class="policy__section"><h2>3. Orders &amp; Payment</h2><p>When you place an order, you are making an offer to purchase the item(s). We will confirm acceptance of your order by sending a confirmation email. A binding contract is formed once payment has been successfully processed and you receive this confirmation. Payments are processed securely via Stripe. We do not store full card details on our servers. If an item becomes unavailable after you place an order, we will notify you promptly and issue a full refund.</p></div>
            <div class="policy__section"><h2>4. Delivery</h2><p>We offer the following delivery options: Standard UK Delivery (3&ndash;5 working days, &pound;3.95), Express UK Delivery (1&ndash;2 working days, &pound;6.95), and International Delivery (7&ndash;14 working days, &pound;12.95). Delivery times are estimates and not guaranteed. Risk of loss passes to you upon delivery. If your order has not arrived within the estimated timeframe, please contact us and we will investigate.</p></div>
            <div class="policy__section"><h2>5. Right to Cancel &amp; Returns</h2><p>Under the Consumer Contracts (Information, Cancellation and Additional Charges) Regulations 2013, you have the right to cancel your order within 14 days of receiving your item without giving any reason. Please see our <a href="/returns-policy">Returns Policy</a> for full details on how to exercise this right, return conditions, and refund timelines.</p></div>
            <div class="policy__section"><h2>6. Your Consumer Rights</h2><p>Nothing in these Terms affects your statutory rights under the Consumer Rights Act 2015. Goods must be as described, fit for purpose, and of satisfactory quality. If an item is faulty or not as described, you are entitled to a repair, replacement, or refund as appropriate.</p></div>
            <div class="policy__section"><h2>7. Website Use</h2><p>You may use this website for lawful purposes only. You must not: use the website in any way that breaches applicable law or regulation; attempt to gain unauthorised access to any part of the website or its infrastructure; use the website to transmit unsolicited commercial communications; or reproduce, duplicate, or resell any part of the website without our express written consent.</p></div>
            <div class="policy__section"><h2>8. Intellectual Property</h2><p>All content on this website, including text, images, logos, and design, is owned by or licensed to EDEN RELICS LTD and is protected by copyright and other intellectual property laws. You may not reproduce, distribute, or create derivative works from any content without our prior written permission.</p></div>
            <div class="policy__section"><h2>9. Limitation of Liability</h2><p>To the fullest extent permitted by law, EDEN RELICS LTD shall not be liable for any indirect, incidental, or consequential damages arising from your use of the website or purchase of products. Our total liability for any claim shall not exceed the price paid for the product(s) in question. Nothing in these Terms excludes or limits liability for death or personal injury caused by negligence, fraud, or any other liability that cannot be excluded by law.</p></div>
            <div class="policy__section"><h2>10. Data Protection</h2><p>Your personal data is handled in accordance with our <a href="/privacy-policy">Privacy Policy</a> and the UK General Data Protection Regulation (UK GDPR). By using our website, you consent to such processing and you warrant that all data provided by you is accurate.</p></div>
            <div class="policy__section"><h2>11. Dispute Resolution</h2><p>If you have a complaint about your order, please contact us at edenrelics&#64;dcp-net.com. We will endeavour to resolve your complaint promptly. If we cannot reach a resolution, you may refer the matter to an alternative dispute resolution (ADR) provider. The European Commission also provides an online dispute resolution platform at ec.europa.eu/consumers/odr. These Terms are governed by the laws of England and Wales, and any disputes shall be subject to the exclusive jurisdiction of the courts of England and Wales.</p></div>
            <div class="policy__section"><h2>12. Changes to These Terms</h2><p>We may update these Terms from time to time. Changes will be posted on this page with an updated revision date. Your continued use of the website after changes are posted constitutes acceptance of the revised Terms.</p></div>
            <div class="policy__section"><h2>13. Contact Us</h2><p>EDEN RELICS LTD, 30 Vane Close, Norwich, NR7 0US, United Kingdom. Email: edenrelics&#64;dcp-net.com. Phone: +44 (0) 7454 705183.</p></div>
            """,

        // Reports
        ["report.security.updated"] = "April 2026",
        ["report.security.content"] = """
            <div class="policy__section"><h2>1. Our Commitment</h2><p>Keeping your personal details and payment information safe is our top priority. We regularly test our website and systems to make sure everything is as secure as it should be, and we're always looking for ways to do better.</p></div>
            <div class="policy__section"><h2>2. What We Tested</h2><p>We carried out a thorough security check of our entire platform &mdash; including how we handle payments, how we connect to our bank, and how we store and protect your personal information. This involved reviewing the code behind the scenes and testing the site the way a real attacker might try to.</p><p>The good news: we found that the site is built on strong foundations. Your connection to our site is always encrypted, your password is stored securely, and our admin areas are properly locked down. We did find some areas we could tighten up, and we've now fixed all of them.</p></div>
            <div class="policy__section"><h2>3. Areas We Looked At</h2><ul><li>How card payments are processed and kept secure</li><li>How our banking connection is protected</li><li>How your personal data is stored and handled</li><li>How logins and accounts are protected</li><li>How we check information you type into the site</li><li>How our servers and hosting are set up</li></ul></div>
            <div class="policy__section"><h2>4. What We Improved</h2><p>Every issue found during testing has been addressed. Here are the key improvements we made:</p><ul><li>Your account data can only be accessed by you</li><li>Login attempts are now limited to prevent automated guessing</li><li>Signing in with Google, Facebook, or Apple requires proper verification</li><li>All forms on the site now thoroughly check what's entered before accepting it</li><li>We've added extra browser-level protections to prevent misuse of the site</li><li>Error messages no longer reveal any behind-the-scenes details</li></ul></div>
            <div class="policy__section"><h2>5. How We Stay Secure</h2><p>Security isn't a one-off job &mdash; it's something we work on continuously. All data sent between your browser and our site is encrypted, passwords are stored using modern hashing so even we can't read them, and we support two-factor authentication for extra protection. We follow industry best practices and carry out regular reviews to stay ahead of new threats.</p><p>If you ever spot something that doesn't look right, or have any security concerns, please get in touch at <a href="mailto:edenrelics&#64;dcp-net.com">edenrelics&#64;dcp-net.com</a>.</p></div>
            <div class="policy__section"><h2>6. Download Full Report</h2><p>You can download a more detailed summary of our security assessment as a PDF.</p><a href="/Eden_Relics_Security_Assessment.pdf" download class="policy__download">Download Security Report (PDF)</a></div>
            """,

        ["report.accessibility.updated"] = "April 2026",
        ["report.accessibility.content"] = """
            <div class="policy__section"><h2>About This Report</h2><p>This report summarises the results of an accessibility audit of the Eden Relics website. The audit checks whether the site can be used comfortably by everyone, including people who rely on screen readers, keyboard navigation, or other assistive technologies.</p><p>We tested seven key pages &mdash; the home page, login, registration, contact form, blog, shopping cart, and privacy policy &mdash; using a combination of automated scanning tools and a detailed manual review of the underlying code.</p><p>The site was assessed against <strong>WCAG 2.1 Level AA</strong>, the internationally recognised standard for web accessibility adopted by governments and organisations worldwide.</p></div>
            <div class="policy__section"><h2>Overall Assessment</h2><p><strong>Verdict: Fully compliant with WCAG 2.1 Level AA.</strong> The automated scan returned <strong>zero errors</strong> across all seven pages tested. All findings from the manual review have been addressed. The site meets the internationally recognised standard for web accessibility.</p></div>
            <div class="policy__section"><h2>Accessibility Features</h2><ul><li><strong>Skip navigation</strong> &mdash; a hidden link lets keyboard users jump straight past the menu to the main content.</li><li><strong>Clear page structure</strong> &mdash; proper HTML elements so screen readers present a logical outline.</li><li><strong>Image descriptions</strong> &mdash; all product images and the logo have text descriptions for screen readers.</li><li><strong>Visible focus indicator</strong> &mdash; a clear outline shows which element is selected when navigating by keyboard.</li><li><strong>Proper form labels</strong> &mdash; every form field has an associated label, with error messages linked to the relevant field.</li><li><strong>Browser auto-fill support</strong> &mdash; form fields allow browsers to auto-fill personal details.</li><li><strong>Meaningful page titles</strong> &mdash; each page has a unique, descriptive title.</li><li><strong>Readable fonts</strong> &mdash; text loads immediately using the system font.</li><li><strong>Stable layout</strong> &mdash; images include dimensions that prevent the page jumping as content loads.</li><li><strong>Keyboard-accessible pop-ups</strong> &mdash; focus is trapped within pop-up windows while they are open, with Escape key support to close them.</li><li><strong>Screen reader announcements</strong> &mdash; dynamic updates such as cart changes and filter results are announced to assistive technologies.</li><li><strong>Sufficient colour contrast</strong> &mdash; all text, including labels on product card images, meets the minimum contrast ratio.</li><li><strong>Touch-friendly controls</strong> &mdash; all interactive elements meet the recommended 44&times;44px minimum touch target size.</li><li><strong>Accessible icons</strong> &mdash; decorative icons are hidden from screen readers, and icon-only buttons include text labels.</li></ul></div>
            <div class="policy__section"><h2>Ongoing Commitment</h2><p>We regularly review our site for accessibility as we add new features. If you experience any difficulty using the site, please contact us at <a href="mailto:edenrelics&#64;dcp-net.com">edenrelics&#64;dcp-net.com</a> and we will do our best to help.</p></div>
            """,

        ["report.compliance.updated"] = "April 2026",
        ["report.compliance.content"] = """
            <div class="policy__section"><h2>Executive Summary</h2><p><strong>Overall status: Compliant across all critical areas.</strong> A full compliance audit was conducted against UK legal requirements for e-commerce businesses. Five critical gaps were identified and all have been remediated. The company now meets its obligations under the Companies Act 2006, UK GDPR, Consumer Rights Act 2015, Consumer Contracts Regulations 2013, E-Commerce Regulations 2002, PECR, and the Modern Slavery Act 2015.</p><p>This report is prepared for the shareholders and directors of EDEN RELICS LTD to provide assurance that the Eden Relics e-commerce platform operates in compliance with applicable UK legislation and industry standards.</p></div>
            <div class="policy__section"><h2>1. Company Information Disclosure</h2><p><strong>Status: Compliant.</strong> All required information (company name, registration number, registered address, contact details) is displayed in the footer of every page and has been verified against Companies House records.</p></div>
            <div class="policy__section"><h2>2. Data Protection &amp; Privacy</h2><p><strong>Status: Compliant (remediated April 2026).</strong> The privacy policy was substantially rewritten to meet UK GDPR requirements. A PECR-compliant cookie consent mechanism has been implemented. Self-service data export and account deletion have been added to the account settings page.</p></div>
            <div class="policy__section"><h2>3. Consumer Protection</h2><p><strong>Status: Compliant.</strong> A detailed returns policy is in place with the 14-day cooling-off period. Right-of-withdrawal is shown at checkout. Comprehensive Terms &amp; Conditions have been published.</p></div>
            <div class="policy__section"><h2>4. Security &amp; Payment Compliance</h2><p><strong>Status: Secure.</strong> Two independent security assessments have been conducted. All identified vulnerabilities have been remediated and verified. Payments are processed via Stripe with no card data stored on our servers.</p></div>
            <div class="policy__section"><h2>5. Accessibility</h2><p><strong>Status: Compliant.</strong> A WCAG 2.1 Level AA audit returned zero automated errors and all manual review findings have been addressed. The site meets the internationally recognised standard for web accessibility.</p></div>
            <div class="policy__section"><h2>6. Ethical &amp; Supply Chain Compliance</h2><p><strong>Status: Compliant.</strong> A Modern Slavery Statement and a Supply Chain Policy are published and linked from the site footer.</p></div>
            <div class="policy__section"><h2>Disclaimer</h2><p>This report has been prepared based on a technical review and is not a substitute for professional legal advice. EDEN RELICS LTD should engage qualified legal counsel to confirm that all policies and practices meet the specific requirements of applicable legislation.</p></div>
            """,

        ["policy.cookies.updated"] = "April 2026",
        ["policy.cookies.content"] = """
            <div class="policy__section"><h2>1. What Are Cookies?</h2><p>Cookies are small text files that are placed on your device when you visit a website. They are widely used to make websites work more efficiently and to provide information to the website owner.</p></div>
            <div class="policy__section"><h2>2. How We Use Cookies</h2><p>We use cookies on the Eden Relics website for the following purposes:</p></div>
            <div class="policy__section"><h2>3. Essential Cookies</h2><p>These cookies are necessary for the website to function and cannot be switched off. They include cookies for: maintaining your shopping cart session, keeping you signed in during your visit, remembering your cookie consent preferences, and protecting against cross-site request forgery. These cookies do not store any personally identifiable information.</p></div>
            <div class="policy__section"><h2>4. Non-Essential Cookies</h2><p>With your consent, we may use non-essential cookies for: social login functionality (Google, Facebook, Apple sign-in), which may set their own cookies to enable authentication. We do not currently use analytics or advertising cookies. If this changes, we will update this policy and request your consent.</p></div>
            <div class="policy__section"><h2>5. Third-Party Cookies</h2><p>When you use social login features, third-party providers (Google, Facebook, Apple) may set cookies on your device. These cookies are governed by the respective provider's cookie and privacy policies. These third-party scripts are only loaded when you have accepted non-essential cookies.</p></div>
            <div class="policy__section"><h2>6. Managing Your Preferences</h2><p>When you first visit our website, you will be shown a cookie consent banner where you can choose to accept all cookies or only essential cookies. You can change your preferences at any time by clearing your browser cookies and revisiting the site, which will trigger the consent banner again. You can also control cookies through your browser settings. Please note that disabling essential cookies may affect the functionality of the website.</p></div>
            <div class="policy__section"><h2>7. Your Rights</h2><p>Under the Privacy and Electronic Communications Regulations (PECR) and UK GDPR, you have the right to choose whether to accept non-essential cookies. We will not set non-essential cookies without your explicit consent.</p></div>
            <div class="policy__section"><h2>8. Contact Us</h2><p>If you have questions about our use of cookies, please contact us at edenrelics&#64;dcp-net.com or by post at EDEN RELICS LTD, 30 Vane Close, Norwich, NR7 0US, United Kingdom.</p></div>
            """,
    };
}
