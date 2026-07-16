namespace Eden_Relics_BE.Data.Entities;

/// <summary>
/// One entry in the curated "Our Top Picks" edit: a product plus its display order and whether it
/// appears on the homepage strip. The list is admin-curated (admin Top Picks tab) and replaced
/// wholesale on save, so entries carry no audit value — hence <see cref="IHardDeletable"/>: removed
/// picks are physically deleted rather than piling up as soft-deleted rows.
///
/// Membership is by <see cref="ProductId"/> (the globally-unique product key) rather than SKU, so a
/// pick is unambiguous across sellers once the marketplace is live — SKUs are only unique per seller.
/// The product is resolved against the live catalogue at read time, so a pick that sells out or is
/// unpublished simply drops off until re-curated. Only membership lives here — the edit's title/intro/
/// meta stay in the frontend collection profile.
/// </summary>
public class TopPick : BaseEntity, IHardDeletable
{
    public Guid ProductId { get; set; }

    /// <summary>Display order within the edit (0-based). Lower shows first.</summary>
    public int Position { get; set; }

    /// <summary>When true, this pick also appears on the homepage "Our Top Picks" strip.</summary>
    public bool Featured { get; set; }
}
