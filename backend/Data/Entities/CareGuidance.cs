namespace Eden_Relics_BE.Data.Entities;

/// <summary>
/// Expert-written advice for a specific fabric × problem combination (e.g. "removing
/// yellowing from silk"). Powers the interactive care finder. Only ExpertApproved rows
/// are served; otherwise the finder composes a general fallback from the fabric + issue
/// guides. These are tool results, not indexed pages, so there's no scaled-content risk.
/// Unique on (FabricId, IssueId).
/// </summary>
public class CareGuidance : BaseEntity
{
    public Guid FabricId { get; set; }
    public Guid IssueId { get; set; }
    public CareSafety Safety { get; set; } = CareSafety.Unknown;
    public string ShortAnswer { get; set; } = "";
    public string SpecificMethod { get; set; } = "";
    public CareReviewStatus Status { get; set; } = CareReviewStatus.Draft;
}
