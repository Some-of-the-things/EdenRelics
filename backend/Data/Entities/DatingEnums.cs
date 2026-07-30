namespace Eden_Relics_BE.Data.Entities;

/// <summary>
/// A kind of dating evidence. Deliberately open-ended: the brand label is one optional
/// input among many, because a large share of real vintage stock has it cut out. A
/// garment with no brand label but a datable care label and a datable zip must still
/// flow through and produce a bounded era.
/// </summary>
public enum EvidenceType
{
    Other = 0,
    CareLabel = 1,
    BrandLabel = 2,
    Zip = 3,
    Construction = 4,
    Fabric = 5,
    /// <summary>A telephone number printed on a maker's/retailer's label.</summary>
    PhoneNumber = 6,
    /// <summary>Country-of-origin wording, e.g. "Made in West Germany".</summary>
    OriginText = 7,
    /// <summary>Certification/standards marks, e.g. a Woolmark or BSI kitemark.</summary>
    CertificationMark = 8,
    /// <summary>Union or manufacturer association labels.</summary>
    UnionLabel = 9,
    /// <summary>Size or measurement labelling conventions.</summary>
    SizeLabel = 10,
}

/// <summary>
/// Which edge of the timeline a rule constrains.
/// </summary>
public enum DateBoundType
{
    /// <summary>The garment cannot pre-date this — a leading edge. Never tolerance-widened.</summary>
    NotBefore = 0,
    /// <summary>The garment cannot post-date this — a trailing edge. Tolerance-widened.</summary>
    NotAfter = 1,
}

/// <summary>
/// How much weight a rule's bound carries. Presence is hard; absence is soft.
///
/// A symbol that did not exist before 1980 cannot appear on a 1975 garment — fact, not
/// judgement. But the absence of a care label does NOT prove pre-1966: UK care labelling
/// was never legally mandatory, makers lagged, and labels get cut out.
///
/// Strength determines behaviour, which is why it must be represented explicitly: a HARD
/// contradiction can hold a listing for review, a SOFT one may only lower confidence.
/// </summary>
public enum RuleStrength
{
    Soft = 0,
    Hard = 1,
}

/// <summary>
/// Editorial state of a rule. Research is staged, so unverified rules live in the store
/// alongside verified ones — but they must never affect output.
/// </summary>
public enum RuleStatus
{
    /// <summary>Drafted from a secondary source; MUST NOT influence any assessment.</summary>
    Unverified = 0,
    /// <summary>Traced to a primary source and signed off. Eligible to bound a date.</summary>
    Active = 1,
    /// <summary>Superseded or found to be wrong. Retained for the evidence chain of past assessments.</summary>
    Retired = 2,
}

/// <summary>
/// How a rule decides whether it applies to a piece of evidence. Rules are data, not
/// code, so the predicate has to be expressible as a row.
/// </summary>
public enum EvidenceTestKind
{
    /// <summary>Fires whenever evidence of the rule's type exists at all.</summary>
    Present = 0,
    /// <summary>Fires when the observed value equals TestValue (case-insensitive).</summary>
    ValueEquals = 1,
    /// <summary>Fires when the observed value contains TestValue (case-insensitive).</summary>
    ValueContains = 2,
    /// <summary>Fires when the observed value matches TestValue as a regular expression.</summary>
    ValueMatchesRegex = 3,
}

/// <summary>
/// Whether a record is a machine proposal or human ground truth.
///
/// Proposed measurements, silhouettes and dates stay proposed until a human confirms
/// them. If recognition is later trained on unconfirmed guesses, the result is a machine
/// that confidently repeats our mistakes — and the accuracy guarantee dies before it
/// exists. Only human-confirmed, evidence-backed records are ever ground truth.
/// </summary>
public enum ConfirmationState
{
    Proposed = 0,
    HumanConfirmed = 1,
    Rejected = 2,
}

/// <summary>Overall shape of a dating assessment.</summary>
public enum DatingOutcome
{
    /// <summary>No active rule fired — nothing is known about the date.</summary>
    Unbounded = 0,
    /// <summary>At least one bound survived; Earliest/Latest describe the surviving window.</summary>
    Bounded = 1,
    /// <summary>
    /// The bounds cannot all be true at once. Either the evidence is misread or a claim is
    /// wrong. Surfaced, never silently reconciled.
    /// </summary>
    Contradiction = 2,
}
