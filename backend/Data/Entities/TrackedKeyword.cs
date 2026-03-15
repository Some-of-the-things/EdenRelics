namespace Eden_Relics_BE.Data.Entities;

public class TrackedKeyword : BaseEntity
{
    public required string Keyword { get; set; }
    public required string PageUrl { get; set; }
    public int? LastPosition { get; set; }
    public DateTime? LastCheckedUtc { get; set; }
    public string? Notes { get; set; }
}
