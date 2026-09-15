namespace Eden_Relics_BE.Data.Entities;

/// <summary>
/// One product strip on the homepage, in the order an admin arranged them on the admin Home
/// Sections tab. Two are built in and always exist: <see cref="HomeSectionKinds.TopPicks"/>, whose
/// pieces come from the Top Picks curation and whose visibility follows the Top Picks switch, and
/// <see cref="HomeSectionKinds.LatestCollection"/>, whose pieces come from the frontend collection
/// profile. Any number of <see cref="HomeSectionKinds.Custom"/> sections can be added alongside, each
/// with its own hand-picked pieces in <see cref="ProductIds"/>.
///
/// The list is replaced wholesale on save, so rows carry no audit value — hence
/// <see cref="IHardDeletable"/>, as with <see cref="TopPick"/>.
/// </summary>
public class HomeSection : BaseEntity, IHardDeletable
{
    /// <summary>One of <see cref="HomeSectionKinds"/>.</summary>
    public required string Kind { get; set; }

    /// <summary>Small line above the title, e.g. "Our Latest Collection".</summary>
    public string Eyebrow { get; set; } = "";

    public required string Title { get; set; }

    public string Description { get; set; } = "";

    /// <summary>Label for the button under the strip. No button when empty.</summary>
    public string ButtonText { get; set; } = "";

    /// <summary>Site-relative path the button goes to, e.g. "/shop/sale". Never an external URL.</summary>
    public string ButtonLink { get; set; } = "";

    /// <summary>Display order on the homepage (0-based). Lower shows first.</summary>
    public int Position { get; set; }

    /// <summary>
    /// Whether the strip shows. Ignored for Top Picks, which follows its own switch so there is only
    /// ever one control for taking it down.
    /// </summary>
    public bool Visible { get; set; } = true;

    /// <summary>Hand-picked pieces, in order, as product ID strings. Custom sections only.</summary>
    public List<string> ProductIds { get; set; } = [];
}

public static class HomeSectionKinds
{
    public const string TopPicks = "top-picks";
    public const string LatestCollection = "latest-collection";
    public const string Custom = "custom";

    public static readonly IReadOnlySet<string> All = new HashSet<string> { TopPicks, LatestCollection, Custom };
    public static readonly IReadOnlySet<string> BuiltIn = new HashSet<string> { TopPicks, LatestCollection };
}
