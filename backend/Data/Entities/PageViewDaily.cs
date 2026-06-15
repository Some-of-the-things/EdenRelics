namespace Eden_Relics_BE.Data.Entities;

/// <summary>
/// First-party, cookieless page-view counter. One aggregate row per
/// (Date, Path, IsBot, Country) — incremented as SSR renders are beaconed in from
/// the Cloudflare Worker. No per-request rows and no PII, so it stays GDPR-clean
/// (aggregate, no cookies, no consent needed) while still seeing 100% of renders.
/// Sits between Cloudflare's raw traffic and GA4's consented cohort as a
/// bot-filtered source of truth. Unique on (Date, Path, IsBot, Country).
/// </summary>
public class PageViewDaily : BaseEntity
{
    public DateOnly Date { get; set; }
    public required string Path { get; set; }

    /// <summary>True when our UA/ASN heuristic classified the render as a bot/crawler.</summary>
    public bool IsBot { get; set; }

    /// <summary>ISO 3166-1 alpha-2 country from Cloudflare (request.cf.country), or "ZZ" if unknown.</summary>
    public required string Country { get; set; }

    public int Count { get; set; }
}
