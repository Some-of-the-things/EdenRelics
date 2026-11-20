namespace Eden_Relics_BE.Data.Entities;

/// <summary>
/// A top-level category hub, e.g. "Vintage Designers &amp; Labels" at /designers. Its
/// <see cref="Key"/> is the URL prefix. The three launch hubs are seeded; the frontend has a
/// route per hub, so adding a fourth also needs a frontend route.
/// </summary>
public class CategoryHub : BaseEntity
{
    /// <summary>URL prefix and stable identifier: "designers", "style" or "dresses".</summary>
    public required string Key { get; set; }

    public required string Name { get; set; }

    public int DisplayOrder { get; set; }
}

/// <summary>
/// A page under a hub, e.g. /style/cottagecore. Edited on the admin Categories tab.
///
/// A piece belongs to a sub-category when its name contains one of <see cref="IncludeKeywords"/>
/// and none of <see cref="ExcludeKeywords"/>, unless an admin has added or removed it by hand
/// (<see cref="SubCategoryProduct"/>), which always wins. Counts are worked out from live stock.
/// </summary>
public class SubCategory : BaseEntity
{
    /// <summary>The <see cref="CategoryHub.Key"/> this page sits under. Fixed after creation.</summary>
    public required string HubKey { get; set; }

    public required string Slug { get; set; }

    public required string Name { get; set; }

    /// <summary>Short line under the heading (the era / origin note on designer pages).</summary>
    public string Tagline { get; set; } = "";

    /// <summary>Lead paragraph (the short description).</summary>
    public string Intro { get; set; } = "";

    /// <summary>
    /// Optional further paragraphs, separated by blank lines. When empty, pages that have
    /// written sections kept in the frontend code (see <see cref="ContentKey"/>) show those instead.
    /// </summary>
    public string Body { get; set; } = "";

    public string MetaTitle { get; set; } = "";

    public string MetaDescription { get; set; } = "";

    public int DisplayOrder { get; set; }

    public bool IsVisible { get; set; } = true;

    /// <summary>Lower-case words matched against a piece's name. Any one is enough.</summary>
    public List<string> IncludeKeywords { get; set; } = [];

    /// <summary>Words that keep a piece out even when an include word matches.</summary>
    public List<string> ExcludeKeywords { get; set; } = [];

    /// <summary>
    /// Links a migrated page to the long written sections still held in the frontend code
    /// (designer history and identification tips, style "what to look for" notes). It is the
    /// page's original slug and deliberately does not change when the slug does. Null for pages
    /// created in the admin.
    /// </summary>
    public string? ContentKey { get; set; }
}

/// <summary>
/// A hand-made exception to keyword matching: <see cref="Included"/> true puts a piece in a
/// sub-category its name doesn't match; false keeps one out that it does. Rows are rebuilt as
/// memberships change, so they're hard-deleted.
/// </summary>
public class SubCategoryProduct : BaseEntity, IHardDeletable
{
    public Guid SubCategoryId { get; set; }

    public Guid ProductId { get; set; }

    public bool Included { get; set; }
}

/// <summary>
/// A permanent (301) redirect from an old sub-category URL, written when a slug changes or a
/// sub-category is deleted, so URLs that have built search authority never break.
/// </summary>
public class CategoryRedirect : BaseEntity, IHardDeletable
{
    public required string HubKey { get; set; }

    public required string FromSlug { get; set; }

    /// <summary>Site-relative target, e.g. "/style/whimsigoth" or "/style".</summary>
    public required string ToPath { get; set; }
}
