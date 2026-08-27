namespace Eden_Relics_BE.Services;

/// <summary>
/// Seed default for the curated "Our Top Picks" gate. Independent of <see cref="MarketplaceOptions"/>:
/// Top Picks is a hand-curated selection of the shop's own live products, so it can go live in the
/// current single-seller shop without touching any marketplace surface. While the gate is off, the
/// homepage strip, the /top-picks page and the nav link stay hidden, but admins can still curate the
/// list (via the admin Top Picks tab) ahead of switching it on.
/// Bound from the "TopPicks" configuration section.
///
/// This is only the starting position: once an admin uses the toggle on the Top Picks tab, the
/// choice is stored in <see cref="Data.Entities.TopPicksSetting"/> and that row governs from then on.
/// Changing this value will not override a stored choice — flip it in the admin UI instead.
/// </summary>
public class TopPicksOptions
{
    public const string SectionName = "TopPicks";

    /// <summary>Starting position for the public Top Picks surfaces, used only while no admin choice
    /// has been stored. Default false (gated).</summary>
    public bool Enabled { get; set; }
}
