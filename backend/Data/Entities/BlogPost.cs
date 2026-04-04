namespace Eden_Relics_BE.Data.Entities;

public class BlogPost : BaseEntity
{
    public required string Title { get; set; }
    public required string Slug { get; set; }
    public required string Content { get; set; } // HTML content
    public string? Excerpt { get; set; }
    public string? FeaturedImageUrl { get; set; }
    public string? Author { get; set; }
    public bool Published { get; set; }
    public DateTime? PublishedAtUtc { get; set; }

    /// <summary>Locale -> translated title (e.g., { "fr": "...", "de": "..." })</summary>
    public Dictionary<string, string> TitleTranslations { get; set; } = [];

    /// <summary>Locale -> translated content HTML</summary>
    public Dictionary<string, string> ContentTranslations { get; set; } = [];

    /// <summary>Locale -> translated excerpt</summary>
    public Dictionary<string, string> ExcerptTranslations { get; set; } = [];
}
