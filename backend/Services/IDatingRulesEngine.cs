using Eden_Relics_BE.Data.Entities;

namespace Eden_Relics_BE.Services;

/// <summary>
/// Deterministic dating validation. Produces a bounded era from an evidence set by
/// intersecting the intervals that individual rules permit.
///
/// It does not average, weight or guess. Everything it asserts is traceable to a rule and
/// a source.
/// </summary>
public interface IDatingRulesEngine
{
    /// <summary>
    /// Runs every active rule against the supplied evidence and returns the surviving
    /// window, without persisting anything. Pure over its inputs, so it is safe to call
    /// speculatively while a seller is still editing a listing.
    /// </summary>
    /// <param name="transitionGroups">
    /// Documented coexistence periods. Where two or more rules sharing a group fire
    /// together, the group's period replaces their individual bounds — the one case in
    /// which more evidence widens the window rather than narrowing it. Omit to intersect
    /// everything, which is the correct behaviour when no groups are defined.
    /// </param>
    DatingAssessment Assess(
        IReadOnlyCollection<GarmentEvidence> evidence,
        IReadOnlyCollection<DatingRule> rules,
        DateOnly? claimedEraStart = null,
        DateOnly? claimedEraEnd = null,
        IReadOnlyCollection<DatingTransitionGroup>? transitionGroups = null);

    /// <summary>
    /// Loads the garment's evidence and the active rule set, assesses, persists the result
    /// with its evidence chain, and returns it. Returns null if the garment does not exist.
    /// </summary>
    Task<DatingAssessment?> AssessGarmentAsync(Guid garmentId, CancellationToken ct = default);
}
