namespace Eden_Relics_BE.Data.Entities;

/// <summary>
/// A vintage-fabric care entry (Viyella, rayon, silk, lace…). Powers the public
/// /care/fabric/{slug} page and the care finder. Published (= indexable) only once an
/// expert has reviewed it. See [[project_firstparty_analytics_plan]]'s sibling plan for
/// the wider first-party-content SEO strategy.
/// </summary>
public class CareFabric : BaseEntity
{
    public required string Slug { get; set; }
    public required string Name { get; set; }

    /// <summary>Synonyms / alternate spellings — used for on-site search and image-ID matching.</summary>
    public List<string> AlsoKnownAs { get; set; } = [];

    /// <summary>The search queries this page is written to rank for (shown to the reviewer).</summary>
    public List<string> TargetKeywords { get; set; } = [];

    public string Intro { get; set; } = "";
    public string FiberContent { get; set; } = "";
    public string HowToIdentify { get; set; } = "";

    // Structured care guidance
    public string Washing { get; set; } = "";
    public string Drying { get; set; } = "";
    public string Ironing { get; set; } = "";
    public string Storing { get; set; } = "";
    public string VintageCautions { get; set; } = "";
    public List<string> Dos { get; set; } = [];
    public List<string> Donts { get; set; } = [];

    // SEO
    public string MetaTitle { get; set; } = "";
    public string MetaDescription { get; set; } = "";

    // Editorial / review gate
    public CareReviewStatus Status { get; set; } = CareReviewStatus.Draft;
    /// <summary>Notes for the reviewer — what to check / outstanding actions.</summary>
    public string ReviewNotes { get; set; } = "";
    public string? ReviewedBy { get; set; }
    public DateTime? LastReviewedUtc { get; set; }

    /// <summary>True only once expert-approved. Gates SSR indexing + sitemap inclusion.</summary>
    public bool IsPublished { get; set; }
}
