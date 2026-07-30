namespace Eden_Relics_BE.Data.Entities;

/// <summary>
/// One deterministic dating rule: a test over a piece of evidence that contributes a
/// single permitted bound.
///
/// Rules are DATA, not code. The engine is a generic interval-intersection machine over
/// this table, which is why it can be built to completion before the rules content
/// exists — seed the verified handful and pour the rest in later.
///
/// A rule never guesses a date. It falsifies impossible ones: "you claimed 1970s, but
/// this care label carries a tumble-dryer symbol that did not exist before 1980".
/// </summary>
public class DatingRule : BaseEntity
{
    /// <summary>Stable human-readable identifier, e.g. "CARE_TUMBLE_DRY_SYMBOL". Unique.</summary>
    public required string Code { get; set; }

    /// <summary>
    /// Plain-English statement of what the rule asserts, shown to the seller when the rule
    /// fires. Write it as the reason, not the mechanism.
    /// </summary>
    public required string Description { get; set; }

    /// <summary>Which kind of evidence this rule inspects.</summary>
    public EvidenceType EvidenceType { get; set; }

    /// <summary>How the rule decides whether it applies to an observation.</summary>
    public EvidenceTestKind TestKind { get; set; }

    /// <summary>
    /// Operand for <see cref="TestKind"/>. Ignored for <see cref="EvidenceTestKind.Present"/>.
    /// For a regex test this is the pattern; it is evaluated with a match timeout because
    /// rule content is authored separately from the code that runs it.
    /// </summary>
    public string? TestValue { get; set; }

    public DateBoundType BoundType { get; set; }

    /// <summary>The date the bound falls on.</summary>
    public DateOnly BoundDate { get; set; }

    /// <summary>
    /// Hard for presence bounds, Soft for absence bounds. Determines whether a
    /// contradiction can hold a listing for review or merely lowers confidence.
    /// </summary>
    public RuleStrength Strength { get; set; } = RuleStrength.Hard;

    /// <summary>
    /// Where the bound comes from — a British Standard, a piece of legislation, an
    /// archive. An assessment must be able to say *why*, and this is the "why".
    /// </summary>
    public string SourceCitation { get; set; } = "";

    /// <summary>Unverified rules exist so research can be staged; they never affect output.</summary>
    public RuleStatus Status { get; set; } = RuleStatus.Unverified;

    /// <summary>
    /// Trailing-edge tolerance in months. Regulations have effective dates; labels do not
    /// — printed label stock got used up and garments sat in warehouses, so a garment can
    /// carry an outdated feature for a while after a change.
    ///
    /// Applied to <see cref="DateBoundType.NotAfter"/> only. A leading edge gets none: a
    /// garment cannot carry a format that did not yet exist.
    ///
    /// Null falls back to the configured default, so the tolerance can be tuned globally
    /// against real data without rewriting every rule.
    /// </summary>
    public int? TrailingToleranceMonths { get; set; }

    /// <summary>Free-text notes for the researcher — open questions, conflicting sources.</summary>
    public string ResearchNotes { get; set; } = "";
}
