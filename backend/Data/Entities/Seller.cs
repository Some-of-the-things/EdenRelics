namespace Eden_Relics_BE.Data.Entities;

/// <summary>Moderation/lifecycle state of a seller account on the curated hub.</summary>
public enum SellerApprovalStatus
{
    /// <summary>Applied to join; awaiting admin review. Cannot list or transact.</summary>
    Applied = 0,

    /// <summary>Approved by admin; may list (listings still go through per-listing moderation).</summary>
    Approved = 1,

    /// <summary>Temporarily suspended by admin; listings hidden, cannot transact.</summary>
    Suspended = 2,

    /// <summary>Application rejected.</summary>
    Rejected = 3,
}

/// <summary>
/// A vendor on the multi-seller curated hub. Every <see cref="Product"/> belongs to exactly one
/// Seller. The platform's own first-party stock belongs to the well-known "house" seller
/// (<see cref="HouseSeller"/>) so that pre-marketplace data has a real owner and SellerId can be
/// non-null everywhere. The whole seller surface stays behind the marketplace feature gate until
/// launch — see <c>MarketplaceOptions</c>.
/// </summary>
public class Seller : BaseEntity
{
    /// <summary>Public display / trading name.</summary>
    public required string BusinessName { get; set; }

    /// <summary>URL slug for the public seller profile page (/sellers/{slug}). Globally unique.</summary>
    public string Slug { get; set; } = "";

    /// <summary>Seller story / about copy shown on the profile page (an SEO + backlink asset).</summary>
    public string? Bio { get; set; }

    public string? LogoUrl { get; set; }

    /// <summary>Operational contact address (order/payout notifications).</summary>
    public string? ContactEmail { get; set; }

    public SellerApprovalStatus ApprovalStatus { get; set; } = SellerApprovalStatus.Applied;

    /// <summary>True only for Eden Relics' own first-party stock (the <see cref="HouseSeller"/>).</summary>
    public bool IsHouse { get; set; }

    // --- Stripe Connect (wired in Phase 3) ---

    /// <summary>Stripe Connect (Express) connected-account id, e.g. "acct_...". Null until onboarded.</summary>
    public string? StripeConnectedAccountId { get; set; }

    /// <summary>True once the seller has completed Stripe Connect onboarding (charges + payouts enabled).</summary>
    public bool ConnectOnboardingComplete { get; set; }

    /// <summary>Per-seller commission override (fraction, e.g. 0.10). Null = use the platform default.</summary>
    public decimal? CommissionRate { get; set; }

    // --- Ownership ---

    /// <summary>The user account that operates this seller. Null for the house seller.</summary>
    public Guid? OwnerUserId { get; set; }
    public User? OwnerUser { get; set; }

    public List<Product> Products { get; set; } = [];
}

/// <summary>
/// The well-known "house" seller representing Eden Relics' own first-party stock. All products,
/// order items and sale transactions that predate the marketplace are backfilled onto this seller
/// by the AddSellerTenancy migration, so SellerId is non-null from day one. Its id and slug are
/// fixed constants (referenced by the seed data and the migration backfill).
/// </summary>
public static class HouseSeller
{
    public static readonly Guid Id = Guid.Parse("5e11e400-0000-0000-0000-000000000001");
    public const string Slug = "eden-relics";
    public const string BusinessName = "Eden Relics";
}
