namespace Eden_Relics_BE.Data.Entities;

/// <summary>
/// Editorial state of a care entry. Only <see cref="ExpertApproved"/> entries are ever
/// published/indexed — the review gate is what keeps the resource the right side of
/// "scaled content abuse".
/// </summary>
public enum CareReviewStatus
{
    /// <summary>Empty/started by hand.</summary>
    Draft = 0,
    /// <summary>AI-drafted, awaiting a human expert pass. Must not be published as-is.</summary>
    AiDrafted = 1,
    /// <summary>Reviewed and signed off by a human expert; eligible to publish.</summary>
    ExpertApproved = 2,
}
