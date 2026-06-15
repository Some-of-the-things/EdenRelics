using System.Text.RegularExpressions;

namespace Eden_Relics_BE.Services;

/// <summary>
/// Heuristic bot-vs-human classification for first-party page-view analytics.
/// We have no Cloudflare bot score (Enterprise-only), so this is a User-Agent +
/// network-organisation heuristic — honest best-effort, not a provider score.
/// Cross-check the human numbers against Cloudflare Web Analytics (also bot-filtered).
/// Pure and static so it's trivially unit-testable.
/// </summary>
public static partial class BotClassifier
{
    // Self-identifying crawlers, libraries, and headless tooling.
    [GeneratedRegex(
        @"bot|crawl|spider|slurp|mediapartners|adsbot|bingpreview|facebookexternalhit|whatsapp|telegrambot|" +
        @"headless|phantomjs|puppeteer|playwright|selenium|" +
        @"python-requests|python-urllib|aiohttp|httpx|scrapy|" +
        @"curl|wget|libwww|java/|go-http-client|okhttp|axios|node-fetch|guzzle|" +
        @"semrush|ahrefs|mj12|dotbot|petalbot|dataforseo|screaming frog|lighthouse|gtmetrix|pingdom|uptimerobot|monitor",
        RegexOptions.IgnoreCase, "en-GB")]
    private static partial Regex BotUserAgent();

    // Datacenter / cloud network operators. Real shoppers browse from consumer ISPs and
    // mobile carriers; sustained traffic from cloud orgs is almost always automated.
    [GeneratedRegex(
        @"amazon|aws|google (llc|cloud)|^google$|microsoft|azure|digitalocean|digital ocean|linode|akamai|fastly|" +
        @"ovh|hetzner|contabo|vultr|scaleway|leaseweb|cloudflare|oracle|alibaba|tencent|huawei cloud|" +
        @"censys|shodan|palo alto|datacamp|m247|hostroyale|colocation|colocrossing",
        RegexOptions.IgnoreCase, "en-GB")]
    private static partial Regex DatacenterOrg();

    /// <summary>
    /// Returns true if the render looks automated. A missing User-Agent is treated as a bot.
    /// </summary>
    public static bool IsBot(string? userAgent, string? asOrganization)
    {
        if (string.IsNullOrWhiteSpace(userAgent))
        {
            return true;
        }

        if (BotUserAgent().IsMatch(userAgent))
        {
            return true;
        }

        // A plausible browser UA from a datacenter network is still very likely a bot
        // spoofing a browser, so fall through to the network check.
        if (!string.IsNullOrWhiteSpace(asOrganization) && DatacenterOrg().IsMatch(asOrganization))
        {
            return true;
        }

        return false;
    }
}
