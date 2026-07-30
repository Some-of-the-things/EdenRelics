namespace Eden_Relics_BE.Data.Entities;

/// <summary>
/// One link in an assessment's evidence chain: this rule, matching this observation,
/// contributed this bound.
///
/// The rule's text and citation are COPIED here rather than referenced, so a past
/// assessment still explains itself after a rule is edited, retired or re-dated. An
/// evidence chain that silently changes meaning is not an evidence chain.
/// </summary>
public class DatingAssessmentStep : BaseEntity
{
    public Guid AssessmentId { get; set; }
    public DatingAssessment? Assessment { get; set; }

    /// <summary>The rule as it stood when this ran. Kept for traceability, not as a live link.</summary>
    public Guid RuleId { get; set; }
    public required string RuleCode { get; set; }
    public required string RuleDescription { get; set; }
    public string SourceCitation { get; set; } = "";

    /// <summary>The observation that satisfied the rule's test.</summary>
    public Guid EvidenceId { get; set; }
    public EvidenceType EvidenceType { get; set; }
    public string EvidenceValue { get; set; } = "";

    public DateBoundType BoundType { get; set; }
    public RuleStrength Strength { get; set; }

    /// <summary>The rule's own bound, before tolerance.</summary>
    public DateOnly BoundDate { get; set; }

    /// <summary>
    /// The bound actually used, after trailing-edge tolerance. Stored alongside the raw
    /// bound so the effect of the tolerance parameter stays visible and auditable rather
    /// than being silently baked into the answer.
    /// </summary>
    public DateOnly EffectiveBoundDate { get; set; }

    /// <summary>Months of tolerance applied (0 for a leading edge).</summary>
    public int ToleranceMonthsApplied { get; set; }

    /// <summary>
    /// False when the bound was computed but excluded from the binding intersection —
    /// currently only for SOFT bounds that would otherwise force a contradiction. The step
    /// is still recorded, because "we saw this and set it aside" is part of the reasoning.
    /// </summary>
    public bool AppliedToInterval { get; set; } = true;

    /// <summary>Why a step was set aside, when it was.</summary>
    public string? ExclusionReason { get; set; }
}
