using Eden_Relics_BE.Data.Entities;
using Eden_Relics_BE.Repositories;

namespace Eden_Relics_BE.Services.CrossListing;

public interface ICrossListingService
{
    /// <summary>Every platform's readiness for one piece: what would go, what blocks it, what to paste.</summary>
    Task<CrossListingPreview?> PreviewAsync(Guid productId);

    /// <summary>Pieces a seller may be offered for relist, oldest first. Never anything under the floor.</summary>
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
/// </summary>
public sealed class CrossListingService(
    IRepository<Product> products,
    IRepository<ProductListing> listings,
    IEnumerable<IMarketplaceAdapter> adapters,
    ILogger<CrossListingService> logger) : ICrossListingService
{
    /// <summary>
    /// Vinted's terms ban relisting to push items up search results, and their enforcement is
    /// automated and effectively unappealable. Legitimate reseller practice — relisting stock that
    /// genuinely hasn't sold in months — is supported, but as decision support: the seller is offered
    /// candidates and picks them one at a time (brief §4.3).
    /// </summary>
    private const int RelistOfferDays = 90;

    /// <summary>
    /// The hard floor. Nothing younger than this can be offered for relist at all, so the feature
    /// cannot be turned into a bump machine by a user who doesn't know the rule. Deliberately lower
    /// than the offer threshold: the offer is the normal cadence, this is the line.
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
            if (days < RelistOfferDays)
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

        // Belt and braces on the floor. The offer threshold already excludes these, but this is the
        // rule that keeps a seller's account safe, so it does not depend on one comparison above.
        return [.. candidates.Where(c => c.DaysListed >= RelistFloorDays).OrderByDescending(c => c.DaysListed)];
    }
}
