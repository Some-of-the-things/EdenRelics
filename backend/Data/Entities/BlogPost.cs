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
}
