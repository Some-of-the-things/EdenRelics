namespace EdenRelics.SellerTool.Dating;

/// <summary>Where a dating signal comes from. The brand label is just one type among many — a
/// garment with the brand label cut out still dates fine from care label + zip + origin, etc.
/// (engineering brief §3.1: evidence set → bounded range, NOT label → date).</summary>
public enum EvidenceType
{
    CareLabel,
    BrandLabel,
    Zip,
    Construction,
    Fabric,
    PhoneNumber,
    OriginText,
    RegulatoryMark,
    Sizing,
    Other,
}

/// <summary>Presence bounds are HARD (a symbol that didn't exist before 1980 cannot be on a 1975
/// piece); absence/trend bounds are SOFT (brief §3.3). Strength drives behaviour: a hard
/// contradiction can hold a listing, a soft one only lowers confidence.</summary>
public enum BoundStrength
{
    Hard,
    Soft,
}

/// <summary>Only <see cref="Verified"/> rules ever affect output (brief §3.4).</summary>
public enum RuleStatus
{
    Unverified,
    Verified,
}

/// <summary>
/// WHY we believe a rule — the quality of the evidence behind it (rules doc §0.3).
///
/// Deliberately orthogonal to <see cref="BoundStrength"/>, and both are needed. Strength governs
/// what the engine DOES; provenance records what the claim RESTS ON. A rule can be
/// hard-from-legislation or hard-from-observed-corpus: identical engine behaviour, very different
/// credibility, and only the first is safely insurable.
///
/// Ordered strongest first, so a numeric comparison ranks credibility.
/// </summary>
public enum ProvenanceClass
{
    /// <summary>A statutory instrument, directive or Act, read directly.</summary>
    PrimaryLegislation,
    /// <summary>A British or ISO standard document, read directly.</summary>
    PrimaryStandard,
    /// <summary>A manufacturer or institutional archive holding.</summary>
    PrimaryArchive,
    /// <summary>Trademark, Companies House or patent filings.</summary>
    PrimaryRegistry,
    /// <summary>Academic work, museum publication, dissertation-based fact sheet.</summary>
    SecondaryScholarly,
    /// <summary>Trade press, label-printer guides, established specialist blogs.</summary>
    SecondaryTrade,
    /// <summary>Derived from our own dated garments — the one class competitors cannot replicate.</summary>
    ObservedCorpus,
    /// <summary>Widely repeated, no traceable source. A research lead, never a rule.</summary>
    CommunityConsensus,
}

/// <summary>
/// How a rule decides whether an observation satisfies it.
///
/// <see cref="Feature"/> is the original model: the client has already classified what it saw into a
/// feature code. That works for care symbols, which are recognised visually. It does NOT work for the
/// families that read text off a label — a phone number, a fibre list, an origin line — where the
/// datable content is the raw string itself, and requiring the client to pre-classify it would move
/// the dating logic out of the rules and into the caller.
/// </summary>
public enum MatchKind
{
    /// <summary>Fires when the observed feature code equals the rule's <see cref="DatingRule.Feature"/>.</summary>
    Feature,
    /// <summary>Fires when the observation's raw value contains the rule's pattern (case-insensitive).</summary>
    ValueContains,
    /// <summary>Fires when the observation's raw value matches the rule's pattern as a regex.</summary>
    ValueRegex,
}

/// <summary>
/// A documented period during which two labelling conventions genuinely coexisted (rules doc §0.4).
///
/// Ordinary rules intersect. That is wrong at a changeover: numbered wash tubs and temperature tubs
/// are both attested across the 1980-1986 care-label generations, so a garment carrying both is not a
/// contradiction and is not precisely dated to the changeover instant — it is a garment from the
/// transition. When two or more rules sharing a group fire, this period REPLACES their individual
/// bounds. Widening on co-occurrence, where every other part of the engine narrows, which is why it
/// is a separate code path rather than a larger lag tolerance.
///
/// One firing rule is not a co-occurrence: a lone numbered tub still bounds normally.
/// </summary>
public sealed record TransitionGroup
{
    public required string Code { get; init; }
    public required string Description { get; init; }
    public int PeriodStart { get; init; }
    public int PeriodEnd { get; init; }
    public int TransitionLagMonths { get; init; }
    public string? SourceCitation { get; init; }
    public ProvenanceClass Provenance { get; init; } = ProvenanceClass.CommunityConsensus;
    public RuleStatus Status { get; init; } = RuleStatus.Unverified;
}

/// <summary>
/// One observed feature on a garment, e.g. feature "care.tumble-dry-symbol" of type CareLabel.
///
/// <paramref name="RawValue"/> carries the text actually read off the label where there is one — the
/// phone number, the fibre list, the origin line. Rules matching on value read it; feature-matching
/// rules ignore it. It is optional because most care-symbol evidence has no meaningful text.
/// </summary>
public sealed record Evidence(string Feature, EvidenceType Type, string? RawValue = null);

/// <summary>
/// A dating rule = data, not code (brief §3.4). When <see cref="Feature"/> is observed, the rule
/// constrains the garment's date to <see cref="NotBefore"/>..<see cref="NotAfter"/>. A rule may set
/// one bound or both (e.g. CC41 = 1941–1952). The leading edge (NotBefore) is firm; the trailing edge
/// (NotAfter) gets a transition-lag tolerance (§3.7).
/// </summary>
public sealed record DatingRule
{
    public required string Id { get; init; }

    /// <summary>
    /// The rule's identifier in Teodora's research document, e.g. "CARE-04". The document is the
    /// source of truth for content and is revised independently of the code, so a stable two-way
    /// reference is what makes reviewing one against the other possible. Several rules may share a
    /// SpecId where one documented rule bounds both ends of a range.
    /// </summary>
    public string SpecId { get; init; } = "";

    /// <summary>The observed feature code that triggers this rule (when <see cref="Match"/> is Feature).</summary>
    public required string Feature { get; init; }

    /// <summary>How this rule decides whether an observation satisfies it.</summary>
    public MatchKind Match { get; init; } = MatchKind.Feature;

    /// <summary>
    /// Operand for the value-matching kinds; ignored for <see cref="MatchKind.Feature"/>. Rule content
    /// is authored separately from the code that runs it, so regexes are evaluated with a timeout.
    /// </summary>
    public string? Pattern { get; init; }

    public EvidenceType Type { get; init; } = EvidenceType.Other;

    /// <summary>Earliest possible year (NOT BEFORE). The garment cannot predate a feature's existence.</summary>
    public int? NotBefore { get; init; }

    /// <summary>Latest possible year (NOT AFTER), before transition-lag tolerance is applied.</summary>
    public int? NotAfter { get; init; }

    public BoundStrength Strength { get; init; } = BoundStrength.Hard;

    /// <summary>Trailing-edge tolerance in months: how long an outdated feature can linger (label
    /// stock used up, warehouse time). Applied to <see cref="NotAfter"/> only; never to NotBefore.</summary>
    public int TransitionLagMonths { get; init; }

    public string? SourceCitation { get; init; }

    /// <summary>
    /// The QUALITY of that source, as a class the engine can compare — orthogonal to
    /// <see cref="Strength"/>. Only hard bounds with primary provenance are safely insurable, so the
    /// guarantee needs this per rule, not just in the research document. Defaults to the weakest
    /// class so an unclassified rule can never be mistaken for a well-sourced one.
    /// </summary>
    public ProvenanceClass Provenance { get; init; } = ProvenanceClass.CommunityConsensus;

    /// <summary>
    /// Optional membership of a named <see cref="TransitionGroup"/>, e.g. "CARE-1986". When two or
    /// more rules sharing a group fire together, the group's period replaces their bounds.
    /// </summary>
    public string? TransitionGroup { get; init; }

    public RuleStatus Status { get; init; } = RuleStatus.Unverified;
}

/// <summary>A closed year interval; null bounds mean "unbounded on that side".</summary>
public readonly record struct DateInterval(int? Earliest, int? Latest)
{
    public static readonly DateInterval Unbounded = new(null, null);

    /// <summary>An interval is empty (impossible) when its lower bound is above its upper bound.</summary>
    public bool IsEmpty => Earliest is int e && Latest is int l && e > l;

    public DateInterval Intersect(DateInterval other) =>
        new(MaxNullable(Earliest, other.Earliest), MinNullable(Latest, other.Latest));

    public bool Overlaps(DateInterval other) => !Intersect(other).IsEmpty;

    public override string ToString() =>
        (Earliest, Latest) switch
        {
            (int e, int l) => e == l ? $"{e}" : $"{e}–{l}",
            (int e, null) => $"{e}+",
            (null, int l) => $"–{l}",
            _ => "unknown",
        };

    private static int? MaxNullable(int? a, int? b) => a is null ? b : b is null ? a : Math.Max(a.Value, b.Value);
    private static int? MinNullable(int? a, int? b) => a is null ? b : b is null ? a : Math.Min(a.Value, b.Value);
}

/// <summary>
/// One rule's contribution to a result — the evidence chain (brief §3.5): every claim can say which
/// rule and which source produced it, and on what authority.
///
/// <paramref name="Applied"/> is false for a bound that was computed but excluded — currently only a
/// rule superseded by its transition group. It stays in the chain because "we saw this and set it
/// aside, for this reason" is part of the reasoning, not noise.
/// </summary>
public sealed record RuleContribution(
    string RuleId,
    string Feature,
    string Bound,
    BoundStrength Strength,
    string? Source,
    ProvenanceClass Provenance = ProvenanceClass.CommunityConsensus,
    string SpecId = "",
    bool Applied = true,
    string? ExclusionReason = null);

/// <summary>Set when a seller's claimed era conflicts with the evidence. Hard = contradicts firm
/// evidence (can hold the listing); Soft = conflicts only with softer signals (lower confidence).</summary>
public sealed record ClaimFlag(BoundStrength Strength, string Message);

public enum DatingOutcome
{
    /// <summary>A consistent estimate was produced.</summary>
    Estimated,

    /// <summary>Hard evidence itself intersects to nothing — misread or fake (brief §3.2).</summary>
    HardContradiction,

    /// <summary>Hard evidence is consistent, but a soft signal conflicts with it.</summary>
    SoftContradiction,
}

/// <summary>The engine's output: the surviving date range, the outcome, the evidence chain, and —
/// if a claim was supplied — whether it conflicts.</summary>
public sealed record DatingResult(
    DateInterval Range,
    DatingOutcome Outcome,
    IReadOnlyList<RuleContribution> Evidence,
    ClaimFlag? ClaimFlag);
