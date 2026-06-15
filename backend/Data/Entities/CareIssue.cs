namespace Eden_Relics_BE.Data.Entities;

/// <summary>
/// A vintage-garment problem entry (age yellowing, musty smell, sweat stains, moth holes…).
/// Powers the public /care/problem/{slug} page. Same review gate as <see cref="CareFabric"/>.
/// These problem queries are the weakest-competition, highest-intent SEO targets.
/// </summary>
public class CareIssue : BaseEntity
{
    public required string Slug { get; set; }
    public required string Name { get; set; }

    public List<string> AlsoKnownAs { get; set; } = [];

    /// <summary>The search queries this page is written to rank for (shown to the reviewer).</summary>
    public List<string> TargetKeywords { get; set; } = [];

    public string Causes { get; set; } = "";
    public string GeneralMethod { get; set; } = "";
    public string WhatNotToDo { get; set; } = "";
    public string WhenToSeeAPro { get; set; } = "";

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
