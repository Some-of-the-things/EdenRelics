namespace Eden_Relics_BE.Services.CrossListing;

/// <summary>
/// How a listing physically reaches a platform. Hybrid by design (brief §3): API/cloud where a real
/// API exists, extension only where we're forced.
///
/// The distinction is not cosmetic. A server API works 24/7 regardless of the seller's machine; an
/// extension needs their browser open and computer awake. The documented failure that follows is an
/// item selling at 3am with the laptop shut, no sync, and a double-sale the seller wakes up to —
/// the single thing they abandon a tool over. So sale detection is driven from the channels we can
/// watch continuously and propagated outward, and extension platforms are honest about the gap.
/// </summary>
public enum ListingTransport
{
    /// <summary>Etsy, eBay — a real public API, driven from our server.</summary>
    ServerApi,

    /// <summary>Vinted, Depop — no legitimate server-side route; the seller's own browser session.</summary>
    SellerBrowserExtension,
}

/// <summary>
/// Whether we actually know this platform's field requirements yet.
///
/// Brief §6 makes field mapping Teodora's research task: field names, required/optional, allowed
/// values, character limits, image rules, category taxonomy and quirks, recorded by someone who
/// lists on all four. Until that exists for a platform, we do not know enough to post — and the same
/// rule as the dating engine applies: an unresearched mapping must never affect output, because a
/// tool that silently posts a malformed listing is worse than one that admits it cannot.
/// </summary>
public enum FieldMappingResearch
{
    /// <summary>Requirements confirmed (from formal API docs, or by listing by hand and recording).</summary>
    Documented,

    /// <summary>Not yet researched. Blocks publishing; preview still explains what is missing.</summary>
    Unresearched,
}

/// <summary>One reason a listing can't go out as-is, or a caution about how it will land.</summary>
/// <param name="Field">The internal field at fault, or the platform field it maps to.</param>
/// <param name="Problem">What is wrong, in words the seller can act on.</param>
/// <param name="Fix">What to do about it, where there is a concrete answer.</param>
public sealed record ListingProblem(string Field, string Problem, string? Fix = null);

/// <summary>
/// What a platform would do with this listing right now.
///
/// <paramref name="Blocking"/> means we refuse to publish. <paramref name="Warnings"/> means it will
/// go out but the seller should know something (a truncated title, a missing measurement).
/// </summary>
public sealed record ListingValidation(
    IReadOnlyList<ListingProblem> Blocking,
    IReadOnlyList<ListingProblem> Warnings)
{
    public bool CanPublish => Blocking.Count == 0;

    public static ListingValidation Ok() => new([], []);
}

/// <summary>
/// A platform's readiness for one listing: whether it can go, the mapped field values, and — always,
/// for extension platforms — copy the seller can paste by hand.
///
/// The paste fallback is not a nicety. Brief §3: "the extension must fail visibly: if a Vinted post
/// or delist doesn't complete, tell the seller and hand them the listing content to paste manually.
/// Never let them believe something went live that didn't." That is only possible if the content
/// exists independently of the extension succeeding, so it is produced up front.
/// </summary>
public sealed record PlatformPlan(
    string Platform,
    ListingTransport Transport,
    FieldMappingResearch Research,
    ListingValidation Validation,
    IReadOnlyDictionary<string, string> Fields,
    PasteFallback? Fallback);

/// <summary>Listing content in a form a human can copy into a platform's own form.</summary>
public sealed record PasteFallback(string Title, string Description, decimal Price);

/// <summary>
/// Translates the internal record into one platform's fields, and says whether it may go.
///
/// Adapters do not publish. Deciding whether to publish, recording the attempt and driving the
/// transport are the service's job, so that an adapter stays a pure, testable mapping — which is
/// what lets Teodora's research land as edits to one small class per platform.
/// </summary>
public interface IMarketplaceAdapter
{
    /// <summary>Platform name, matching the strings already used by ProductListing.Platform.</summary>
    string Platform { get; }

    ListingTransport Transport { get; }

    FieldMappingResearch Research { get; }

    /// <summary>Whether this listing may be published to this platform, and anything worth saying.</summary>
    ListingValidation Validate(CanonicalListing listing);

    /// <summary>The platform's field values for this listing. Only meaningful when validation passes.</summary>
    IReadOnlyDictionary<string, string> MapFields(CanonicalListing listing);

    /// <summary>Human-pasteable content. Required for extension platforms; null where it makes no sense.</summary>
    PasteFallback? BuildFallback(CanonicalListing listing);
}
