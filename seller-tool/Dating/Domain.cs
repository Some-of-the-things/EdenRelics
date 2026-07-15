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

/// <summary>One observed feature on a garment, e.g. feature "care.tumble-dry-symbol" of type
/// CareLabel. The engine keys on the feature code.</summary>
public sealed record Evidence(string Feature, EvidenceType Type);

/// <summary>
/// A dating rule = data, not code (brief §3.4). When <see cref="Feature"/> is observed, the rule
/// constrains the garment's date to <see cref="NotBefore"/>..<see cref="NotAfter"/>. A rule may set
/// one bound or both (e.g. CC41 = 1941–1952). The leading edge (NotBefore) is firm; the trailing edge
/// (NotAfter) gets a transition-lag tolerance (§3.7).
/// </summary>
public sealed record DatingRule
{
    public required string Id { get; init; }

    /// <summary>The observed feature code that triggers this rule.</summary>
    public required string Feature { get; init; }

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

/// <summary>One rule's contribution to a result — the evidence chain (brief §3.5): every claim can
/// say which rule and which source produced it.</summary>
public sealed record RuleContribution(string RuleId, string Feature, string Bound, BoundStrength Strength, string? Source);

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
