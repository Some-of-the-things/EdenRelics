using Eden_Relics_BE.Data.Entities;

namespace Eden_Relics_BE.Services.CrossListing;

/// <summary>
/// One internal listing record, platform-agnostic. Per the crosslisting brief §6 this is the whole
/// substance of a crosslister: "one internal listing record, correctly translated into each
/// platform's fields." Everything platform-specific belongs in an adapter, never here.
///
/// Built from a shop Product today (we are user zero — brief §10, "Teodora is beta tester zero"),
/// but deliberately not coupled to it: the seller tool's Garment will produce the same record once
/// sellers are onboarded.
/// </summary>
public sealed record CanonicalListing
{
    public required Guid SourceId { get; init; }
    public required string Sku { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }

    /// <summary>What we'd actually charge — the sale price when one is set, else the full price.</summary>
    public required decimal Price { get; init; }

    /// <summary>Free text as authored, e.g. "1970s". Not trusted to be a parseable decade.</summary>
    public required string Era { get; init; }

    public required string Category { get; init; }
    public required string Size { get; init; }
    public required string Condition { get; init; }
    public string? Material { get; init; }
    public string? Brand { get; init; }

    /// <summary>Primary image first. Order matters on every platform.</summary>
    public required IReadOnlyList<string> ImageUrls { get; init; }

    /// <summary>
    /// Garment measurements in cm. Empty until the measurement tool lands (brief §5). Kept as a
    /// dictionary because platforms want them as free text in wildly different shapes, and because a
    /// measurement is only publishable once a human has confirmed it — see
    /// <see cref="MeasurementsConfirmed"/>.
    /// </summary>
    public IReadOnlyDictionary<string, decimal> MeasurementsCm { get; init; } =
        new Dictionary<string, decimal>();

    /// <summary>
    /// False for a machine proposal. Brief §5: "never publish an unconfirmed machine measurement to a
    /// live listing" — wrong measurement, doesn't fit, return, and returns are lethal to a business
    /// whose pitch is that you can trust what it says.
    /// </summary>
    public bool MeasurementsConfirmed { get; init; }

    /// <summary>When this piece was first listed anywhere, for relist eligibility (brief §4.3).</summary>
    public DateTime? FirstListedAtUtc { get; init; }

    public static CanonicalListing FromProduct(Product product, DateTime? firstListedAtUtc = null) => new()
    {
        SourceId = product.Id,
        Sku = product.Sku,
        Title = product.Name,
        Description = product.Description,
        // A listing must quote what the buyer pays, not the pre-reduction price.
        Price = product.SalePrice ?? product.Price,
        Era = product.Era,
        Category = product.Category,
        Size = product.Size,
        Condition = product.Condition,
        Material = product.Material,
        // The shop has no separate brand field; the designer is carried in the name.
        Brand = null,
        ImageUrls = string.IsNullOrWhiteSpace(product.ImageUrl)
            ? [.. product.AdditionalImageUrls]
            : [product.ImageUrl, .. product.AdditionalImageUrls],
        FirstListedAtUtc = firstListedAtUtc,
    };
}
