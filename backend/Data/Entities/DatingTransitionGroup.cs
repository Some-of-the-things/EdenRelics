namespace Eden_Relics_BE.Data.Entities;

/// <summary>
/// A documented period during which two labelling conventions genuinely coexisted.
///
/// Ordinary rules intersect: each contributes a permitted interval and the estimate is
/// what survives all of them. That is wrong at a changeover. Numbered wash tubs and
/// temperature tubs are both attested across the 1980-1986 care-label generations, even
/// alternating between the 1980 and 1982 codes — so a garment carrying both is not a
/// contradiction and is not precisely dated to the changeover instant. It is a garment
/// from the transition.
///
/// So when two or more rules sharing a group both fire, the engine replaces their
/// individual bounds with this period. Widening on co-occurrence, where every other part
/// of the engine narrows — which is why it is a separate code path rather than a larger
/// tolerance. A tolerance would still be pulling towards a single date; this asserts a
/// different fact about the world.
///
/// One firing rule is NOT a transition: a single numbered tub still bounds normally. The
/// group only takes over when the co-occurrence itself is the evidence.
/// </summary>
public class DatingTransitionGroup : BaseEntity
{
    /// <summary>Stable identifier used by <see cref="DatingRule.TransitionGroupCode"/>, e.g. "CARE-1986".</summary>
    public required string Code { get; set; }

    /// <summary>What coexisted, and why that is authentic rather than an error.</summary>
    public required string Description { get; set; }

    /// <summary>Start of the documented coexistence period.</summary>
    public DateOnly PeriodStart { get; set; }

    /// <summary>
    /// End of the documented coexistence period. Trailing tolerance still applies on top,
    /// because the transition period is itself dated from labels.
    /// </summary>
    public DateOnly PeriodEnd { get; set; }

    /// <summary>Trailing tolerance for <see cref="PeriodEnd"/>; null falls back to the configured default.</summary>
    public int? TrailingToleranceMonths { get; set; }

    public string SourceCitation { get; set; } = "";

    public ProvenanceClass Provenance { get; set; } = ProvenanceClass.CommunityConsensus;

    /// <summary>An unverified group must no more affect output than an unverified rule.</summary>
    public RuleStatus Status { get; set; } = RuleStatus.Unverified;
}
