namespace Eden_Relics_BE.Services;

/// <summary>
/// Feature gate for the multi-seller marketplace. The whole seller/marketplace surface (seller
/// onboarding, dashboards, public seller profiles, Connect checkout) is built behind this flag.
/// While <see cref="Enabled"/> is false — the default — the site behaves exactly as the original
/// single-seller shop and nothing marketplace-related is publicly reachable. Flip to true only at
/// launch. Bound from the "Marketplace" configuration section.
/// </summary>
public class MarketplaceOptions
{
    public const string SectionName = "Marketplace";

    /// <summary>Master on/off switch for every marketplace surface. Default false (gated).</summary>
    public bool Enabled { get; set; }
}
