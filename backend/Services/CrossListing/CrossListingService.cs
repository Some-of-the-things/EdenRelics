using Eden_Relics_BE.Data.Entities;
using Eden_Relics_BE.Repositories;

namespace Eden_Relics_BE.Services.CrossListing;

public interface ICrossListingService
{
    /// <summary>Every platform's readiness for one piece: what would go, what blocks it, what to paste.</summary>
    Task<CrossListingPreview?> PreviewAsync(Guid productId);

    /// <summary>
    /// Relist-eligible pieces, oldest first, in answer to someone asking. Never anything under the
    /// floor, and never used to prompt — see the note on <see cref="CrossListingService"/>.
    /// </summary>
    Task<IReadOnlyList<RelistCandidate>> RelistCandidatesAsync();
}

public sealed record CrossListingPreview(
    Guid ProductId, string Sku, string Title, decimal Price, IReadOnlyList<PlatformPlan> Platforms);

public sealed record RelistCandidate(
    Guid ProductId, string Sku, string Title, string Platform, int DaysListed);

/// <summary>
/// Turns one shop piece into per-platform listing plans, and decides what may go out.
///
/// Adapters stay pure mappings; the judgement lives here. Nothing publishes on a blocking problem,
/// and extension platforms always carry pasteable content so an extension failure can never leave
/// the seller with nothing (brief §3).
///
/// RELIST IS PULL-ONLY, AND DELIBERATELY NOT A PROMPT. The brief sketched the tool surfacing "these
/// 12 items have been listed 90+ days — relist any?"; Peter's call (2026-08-07) is that the nudge
/// goes and the capability stays. So <see cref="RelistCandidatesAsync"/> only ever answers a question
/// someone asked. Do not build a notification, badge, banner, digest email or dashboard count on top
/// of it: a tool that repeatedly suggests relisting is one that trains a seller into exactly the
/// machine-timed bumping pattern Vinted's rule against pushing items up search is aimed at, and the
/// consequence lands on their account, not ours.
/// </summary>
public sealed class CrossListingService(
    IRepository<Product> products,
    IRepository<ProductListing> listings,
    IEnumerable<IMarketplaceAdapter> adapters,
    ILogger<CrossListingService> logger) : ICrossListingService
{
    /// <summary>
    /// How long a piece must have sat before it appears in an answer at all. Teodora's own cadence is
    /// roughly three months, and relisting stock that genuinely hasn't sold in months is legitimate
    /// reseller practice — the thing to avoid is doing it on a machine's schedule rather than a
    /// person's judgement, which is why nothing here prompts.
    /// </summary>
    private const int RelistEligibleDays = 90;

    /// <summary>
    /// The hard floor. Nothing younger than this is ever returned, so the feature cannot be turned
    /// into a bump machine by a user who doesn't know the rule. Deliberately lower than
    /// <see cref="RelistEligibleDays"/>: that one is the normal cadence, this one is the line.
    /// </summary>
    internal const int RelistFloorDays = 60;

    public async Task<CrossListingPreview?> PreviewAsync(Guid productId)
    {
        Product? product = await products.GetByIdAsync(productId);
        if (product is null)
        {
            return null;
        }

        IEnumerable<ProductListing> existing = await listings.FindAsync(l => l.ProductId == productId);
        DateTime? firstListed = existing.Any() ? existing.Min(l => l.CreatedAtUtc) : null;

        CanonicalListing listing = CanonicalListing.FromProduct(product, firstListed);

        List<PlatformPlan> plans = [];
        foreach (IMarketplaceAdapter adapter in adapters.OrderBy(a => a.Platform, StringComparer.Ordinal))
        {
            ListingValidation validation = adapter.Validate(listing);
            plans.Add(new PlatformPlan(
                adapter.Platform,
                adapter.Transport,
                adapter.Research,
                validation,
                // Mapping a listing that can't go out would invite acting on it. The problems say why.
                validation.CanPublish ? adapter.MapFields(listing) : new Dictionary<string, string>(),
                adapter.BuildFallback(listing)));
        }

        int publishable = plans.Count(p => p.Validation.CanPublish);
        logger.LogInformation(
            "Cross-listing preview for {Sku}: {Publishable} of {Total} platforms ready",
            product.Sku, publishable, plans.Count);

        return new CrossListingPreview(product.Id, product.Sku, product.Name, listing.Price, plans);
    }

    public async Task<IReadOnlyList<RelistCandidate>> RelistCandidatesAsync()
    {
        DateTime now = DateTime.UtcNow;
        IEnumerable<ProductListing> active = await listings.FindAsync(l => l.Status == "Active");

        List<RelistCandidate> candidates = [];
        foreach (ProductListing listing in active)
        {
            int days = (int)(now - listing.CreatedAtUtc).TotalDays;
            if (days < RelistEligibleDays)
            {
                continue;
            }

            Product? product = await products.GetByIdAsync(listing.ProductId);
            // Only live stock: relisting something already sold would be worse than useless.
            if (product is null || product.Status != ProductStatus.Live)
            {
                continue;
            }
            candidates.Add(new RelistCandidate(product.Id, product.Sku, product.Name, listing.Platform, days));
        }

        // Belt and braces on the floor. The eligibility threshold already excludes these, but this is
        // the rule that keeps a seller's account safe, so it does not depend on one comparison above.
        return [.. candidates.Where(c => c.DaysListed >= RelistFloorDays).OrderByDescending(c => c.DaysListed)];
    }
}
