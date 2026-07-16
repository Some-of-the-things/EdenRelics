namespace Eden_Relics_BE.Services;

/// <summary>
/// Feature gate for the curated "Our Top Picks" edit. Independent of <see cref="MarketplaceOptions"/>:
/// Top Picks is a hand-curated selection of the shop's own live products, so it can go live in the
/// current single-seller shop without touching any marketplace surface. While <see cref="Enabled"/>
/// is false — the default — the homepage strip, the /top-picks page and the nav link stay hidden,
/// but admins can still curate the list (via the admin Top Picks tab) ahead of switching it on.
/// Bound from the "TopPicks" configuration section.
/// </summary>
public class TopPicksOptions
{
    public const string SectionName = "TopPicks";

    /// <summary>Master on/off switch for the public Top Picks surfaces. Default false (gated).</summary>
    public bool Enabled { get; set; }
}
