namespace Eden_Relics_BE.Services.CrossListing;

/// <summary>Checks every platform needs, so no adapter re-implements them differently.</summary>
internal static class CommonChecks
{
    internal const int MinImages = 1;

    internal static void Basics(CanonicalListing l, List<ListingProblem> blocking)
    {
        if (string.IsNullOrWhiteSpace(l.Title))
        {
            blocking.Add(new ListingProblem("Title", "No title.", "Name the piece before listing it."));
        }
        if (string.IsNullOrWhiteSpace(l.Description))
        {
            blocking.Add(new ListingProblem("Description", "No description."));
        }
        if (l.Price <= 0)
        {
            blocking.Add(new ListingProblem("Price", "Price is zero.", "Set a price."));
        }
        if (l.ImageUrls.Count < MinImages)
        {
            blocking.Add(new ListingProblem("Images", "No photographs.", "Add at least one photo."));
        }
    }

    /// <summary>
    /// A machine-proposed measurement must not reach a live listing (brief §5). It is a warning
    /// rather than a block only because no measurements exist yet; once the measurement tool lands,
    /// an unconfirmed proposal on a listing being published is a block.
    /// </summary>
    internal static void Measurements(CanonicalListing l, List<ListingProblem> warnings)
    {
        if (l.MeasurementsCm.Count > 0 && !l.MeasurementsConfirmed)
        {
            warnings.Add(new ListingProblem(
                "Measurements",
                "These measurements are machine proposals nobody has confirmed.",
                "Accept or correct them before this goes live — a wrong measurement means a return."));
        }
    }

    /// <summary>
    /// Every platform sells vintage by decade, and our era field is free text. The same gap put
    /// 1950s and 1960s pieces on Etsy declared as made in the 2000s, so it is checked centrally.
    /// </summary>
    internal static void Era(CanonicalListing l, List<ListingProblem> blocking)
    {
        if (!MarketplaceService.TryMapEraToEtsyWhenMade(l.Era, out _))
        {
            blocking.Add(new ListingProblem(
                "Era",
                $"Era '{l.Era}' does not resolve to a decade.",
                "Set the era to a decade the platforms recognise, e.g. '1970s'."));
        }
    }

    internal static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max].TrimEnd();

    internal static string SizeLine(CanonicalListing l) => $"Size: UK {l.Size}";

    internal static string MeasurementLine(CanonicalListing l) =>
        l.MeasurementsCm.Count == 0
            ? string.Empty
            : string.Join(" · ", l.MeasurementsCm.Select(m => $"{m.Key} {m.Value:0.#}cm"));
}

/// <summary>
/// Etsy. A proper public API (brief §2) and the one platform we already publish to, so its
/// requirements come from their documentation rather than from guesswork.
/// </summary>
public sealed class EtsyAdapter : IMarketplaceAdapter
{
    /// <summary>Etsy rejects titles over 140 characters outright.</summary>
    private const int MaxTitle = 140;

    public string Platform => "Etsy";
    public ListingTransport Transport => ListingTransport.ServerApi;
    public FieldMappingResearch Research => FieldMappingResearch.Documented;

    public ListingValidation Validate(CanonicalListing l)
    {
        List<ListingProblem> blocking = [];
        List<ListingProblem> warnings = [];
        CommonChecks.Basics(l, blocking);
        // when_made is a required Etsy field and a public claim about the garment's age.
        CommonChecks.Era(l, blocking);
        CommonChecks.Measurements(l, warnings);

        if (l.Title.Length > MaxTitle)
        {
            warnings.Add(new ListingProblem(
                "Title", $"Longer than Etsy's {MaxTitle} characters and will be cut short.",
                "Shorten it so the cut lands where you want it."));
        }
        return new ListingValidation(blocking, warnings);
    }

    public IReadOnlyDictionary<string, string> MapFields(CanonicalListing l)
    {
        MarketplaceService.TryMapEraToEtsyWhenMade(l.Era, out string whenMade);
        return new Dictionary<string, string>
        {
            ["quantity"] = "1",
            ["title"] = CommonChecks.Truncate(l.Title, MaxTitle),
            ["description"] = l.Description,
            ["price"] = l.Price.ToString("F2"),
            ["who_made"] = "someone_else",
            ["when_made"] = whenMade,
            // Womenswear > Dresses. Taxonomy work beyond dresses is part of §6 research.
            ["taxonomy_id"] = "1759",
            ["is_supply"] = "false",
            ["type"] = "physical",
        };
    }

    // Server-side: if the API call fails we retry or report it, we don't ask a human to paste.
    public PasteFallback? BuildFallback(CanonicalListing l) => null;
}

/// <summary>
/// eBay. A proper public API (brief §2) and in v1 scope, but the field mapping is not done, so it
/// refuses to publish rather than posting something malformed.
/// </summary>
public sealed class EbayAdapter : IMarketplaceAdapter
{
    public string Platform => "eBay";
    public ListingTransport Transport => ListingTransport.ServerApi;
    public FieldMappingResearch Research => FieldMappingResearch.Unresearched;

    public ListingValidation Validate(CanonicalListing l)
    {
        List<ListingProblem> blocking =
        [
            new ListingProblem(
                "Platform",
                "eBay's field mapping has not been researched yet.",
                "Record eBay's required fields, category taxonomy and limits (brief §6) before listing there."),
        ];
        List<ListingProblem> warnings = [];
        CommonChecks.Basics(l, blocking);
        CommonChecks.Measurements(l, warnings);
        return new ListingValidation(blocking, warnings);
    }

    public IReadOnlyDictionary<string, string> MapFields(CanonicalListing l) =>
        new Dictionary<string, string>();

    public PasteFallback? BuildFallback(CanonicalListing l) => null;
}

/// <summary>
/// Base for the extension platforms. They share the transport, the honest-failure requirement and
/// the fact that their forms have to be mapped by hand rather than read from documentation.
/// </summary>
public abstract class ExtensionAdapter : IMarketplaceAdapter
{
    public abstract string Platform { get; }
    public ListingTransport Transport => ListingTransport.SellerBrowserExtension;
    public abstract FieldMappingResearch Research { get; }

    protected abstract int MaxTitle { get; }

    public virtual ListingValidation Validate(CanonicalListing l)
    {
        List<ListingProblem> blocking = [];
        List<ListingProblem> warnings = [];
        CommonChecks.Basics(l, blocking);
        CommonChecks.Era(l, blocking);
        CommonChecks.Measurements(l, warnings);

        if (Research == FieldMappingResearch.Unresearched)
        {
            blocking.Add(new ListingProblem(
                "Platform",
                $"{Platform}'s form fields have not been mapped yet.",
                $"List one {Platform} item by hand and record what the form asks for (brief §6)."));
        }

        // The seller's machine has to be awake for this to happen at all — say so once, here,
        // rather than letting them discover it when something doesn't sync.
        warnings.Add(new ListingProblem(
            "Transport",
            $"{Platform} has no API, so this posts through your browser and needs it open.",
            "If it doesn't complete you'll get the listing text to paste in yourself."));

        return new ListingValidation(blocking, warnings);
    }

    public abstract IReadOnlyDictionary<string, string> MapFields(CanonicalListing l);

    /// <summary>Always produced, so an extension failure can never leave the seller with nothing.</summary>
    public PasteFallback BuildFallback(CanonicalListing l) =>
        new(CommonChecks.Truncate(BuildTitle(l), MaxTitle), BuildDescription(l), l.Price);

    PasteFallback? IMarketplaceAdapter.BuildFallback(CanonicalListing l) => BuildFallback(l);

    protected abstract string BuildTitle(CanonicalListing l);
    protected abstract string BuildDescription(CanonicalListing l);
}

/// <summary>
/// Vinted. Extension-only: the Pro Integrations API is allowlisted to a few Vinted Pro businesses,
/// appears single-tenant, and our application should be assumed refused (brief §2). Every competitor
/// ships an extension for exactly this reason.
/// </summary>
public sealed class VintedAdapter : ExtensionAdapter
{
    public override string Platform => "Vinted";

    // The form's fields, allowed condition values and size taxonomy still have to be recorded by
    // hand. Flipping this to Documented is Teodora's research landing, not a code change.
    public override FieldMappingResearch Research => FieldMappingResearch.Unresearched;

    protected override int MaxTitle => 100;

    public override IReadOnlyDictionary<string, string> MapFields(CanonicalListing l) =>
        new Dictionary<string, string>
        {
            ["title"] = CommonChecks.Truncate(BuildTitle(l), MaxTitle),
            ["description"] = BuildDescription(l),
            ["price"] = l.Price.ToString("F2"),
            ["size"] = l.Size,
            ["condition"] = l.Condition,
        };

    protected override string BuildTitle(CanonicalListing l) =>
        $"{l.Title} {l.Era} Vintage Size {l.Size}";

    protected override string BuildDescription(CanonicalListing l)
    {
        List<string> parts =
        [
            l.Description,
            "",
            CommonChecks.SizeLine(l),
            $"Era: {l.Era}",
            $"Condition: {l.Condition}",
        ];
        string measurements = CommonChecks.MeasurementLine(l);
        if (measurements.Length > 0)
        {
            parts.Add($"Measurements: {measurements}");
        }
        parts.Add("");
        parts.Add("Also available at edenrelics.co.uk");
        return string.Join("\n", parts);
    }
}

/// <summary>Depop. No public API either (brief §2), so extension, same rules as Vinted.</summary>
public sealed class DepopAdapter : ExtensionAdapter
{
    public override string Platform => "Depop";
    public override FieldMappingResearch Research => FieldMappingResearch.Unresearched;
    protected override int MaxTitle => 65;

    public override IReadOnlyDictionary<string, string> MapFields(CanonicalListing l) =>
        new Dictionary<string, string>
        {
            ["title"] = CommonChecks.Truncate(BuildTitle(l), MaxTitle),
            ["description"] = BuildDescription(l),
            ["price"] = l.Price.ToString("F2"),
            ["size"] = l.Size,
            ["condition"] = l.Condition,
        };

    protected override string BuildTitle(CanonicalListing l) => $"{l.Title} — {l.Era} Vintage";

    protected override string BuildDescription(CanonicalListing l)
    {
        // Hashtags are how Depop is browsed. Mechanical, from the garment's own attributes — the
        // brief rules out anything whose pitch is that a model writes the listing.
        string decade = l.Era.Replace("s", string.Empty).Trim();
        string hashtags = string.Join(' ',
            "#vintage", "#vintagefashion", $"#{l.Category}", $"#{decade}s", "#sustainablefashion");

        List<string> parts =
        [
            l.Description,
            "",
            CommonChecks.SizeLine(l),
            $"Era: {l.Era}",
            $"Condition: {l.Condition}",
        ];
        string measurements = CommonChecks.MeasurementLine(l);
        if (measurements.Length > 0)
        {
            parts.Add($"Measurements: {measurements}");
        }
        parts.Add("");
        parts.Add(hashtags);
        return string.Join("\n", parts);
    }
}
