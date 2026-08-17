using EdenRelics.SellerTool.Dating;

namespace EdenRelics.SellerTool.Data;

public abstract class ToolBaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

/// <summary>Machine-produced records are PROPOSED until a human confirms them (brief §3.6). Only
/// Confirmed records are ever treated as ground truth — we never train on our own guesses.</summary>
public enum ConfirmationState
{
    Proposed,
    Confirmed,
    Rejected,
}

/// <summary>
/// Which shot a captured photo is. Slots exist so the archive is consistent: a pile of
/// inconsistently-framed photos is not a moat, it is a folder.
/// </summary>
public enum CaptureSlot
{
    /// <summary>Not captured against the standard (e.g. an ad-hoc upload).</summary>
    Unspecified,
    BrandLabel,
    CareLabel,
    FlatLayFront,
    FlatLayBack,
    Zip,
    ConstructionDetail,
}

/// <summary>A garment in the archive. Its date comes from its evidence set, not from any single
/// label (brief §3.1), so the brand may be unknown and the piece still fully dated.</summary>
public class Garment : ToolBaseEntity
{
    /// <summary>The authenticated user (seller) who owns this garment. Set from the caller's identity;
    /// non-admins only see/act on their own garments.</summary>
    public Guid OwnerId { get; set; }

    /// <summary>External reference (e.g. the seller's listing id/slug). Optional.</summary>
    public string? Reference { get; set; }

    public string? Title { get; set; }

    /// <summary>Identifier of the seller who owns this garment (kept loose — the tool is decoupled
    /// from the marketplace's Seller ids).</summary>
    public string? SellerRef { get; set; }

    public List<EvidenceRecord> Evidence { get; set; } = [];
    public List<DateEstimate> Estimates { get; set; } = [];
}

/// <summary>One typed piece of dating evidence captured for a garment — a care-label photo, a zip, a
/// phone number on the maker's address, etc. The label images captured here ARE the archive/moat.</summary>
public class EvidenceRecord : ToolBaseEntity
{
    public Guid GarmentId { get; set; }
    public Garment? Garment { get; set; }

    public EvidenceType Type { get; set; }

    /// <summary>The feature code the dating engine matches on, e.g. "care.tumble-dry-symbol".</summary>
    public string Feature { get; set; } = "";

    /// <summary>Optional raw captured value (the phone number, the origin text, …).</summary>
    public string? RawValue { get; set; }

    /// <summary>Storage key for the captured label/photo (e.g. an R2 object key). The archive asset,
    /// stored VERBATIM — the original bytes are the thing with long-term value.</summary>
    public string? ImageKey { get; set; }

    /// <summary>
    /// Storage key for the generated web-sized derivative. Disposable: it can be rebuilt from the
    /// archive original at any time, which is why the original is never resized in place.
    /// </summary>
    public string? DisplayImageKey { get; set; }

    /// <summary>Which shot this is, when captured against the standard.</summary>
    public CaptureSlot Slot { get; set; } = CaptureSlot.Unspecified;

    /// <summary>
    /// Whether the seller granted archive rights AT CAPTURE TIME. Recorded per capture, not per
    /// account, so the archive's provenance survives a seller leaving or the terms changing — the
    /// one asset that has to be unencumbered cannot rest on a flag that might be revoked wholesale.
    /// </summary>
    public bool ArchiveRightsGranted { get; set; }

    /// <summary>The capture standard in force when this was taken, so a later revision stays legible.</summary>
    public string? CaptureStandardVersion { get; set; }

    public int? Width { get; set; }
    public int? Height { get; set; }
    public long? ByteSize { get; set; }
    public string? ContentType { get; set; }

    /// <summary>How it was captured — "machine" (proposed) or "human".</summary>
    public string Origin { get; set; } = "machine";

    public ConfirmationState Confirmation { get; set; } = ConfirmationState.Proposed;
    public string? ConfirmedBy { get; set; }
    public DateTime? ConfirmedAtUtc { get; set; }
}

/// <summary>A derived date bound for a garment, with the evidence chain that produced it (brief §3.5)
/// and a confirmation state (proposed until a human accepts it, §3.6).</summary>
public class DateEstimate : ToolBaseEntity
{
    public Guid GarmentId { get; set; }
    public Garment? Garment { get; set; }

    public int? Earliest { get; set; }
    public int? Latest { get; set; }

    /// <summary>The engine outcome name (Estimated / HardContradiction / SoftContradiction).</summary>
    public string Outcome { get; set; } = "";

    /// <summary>Serialised evidence chain (which rules + sources produced this). Never just the
    /// conclusion — the reasoning is stored so it can be cited and, later, insured.</summary>
    public string EvidenceChainJson { get; set; } = "[]";

    public ConfirmationState Confirmation { get; set; } = ConfirmationState.Proposed;
    public DateTime ComputedAtUtc { get; set; }
}

/// <summary>
/// Something a seller (or the tool on their behalf) did, recorded so the brief's §10 numbers can be
/// answered: listings created, time per listing, measurement acceptance rate, extension failure rate
/// per platform — and the one that decides whether the whole thesis holds, flags raised against how
/// often the seller was actually wrong.
///
/// Deliberately a narrow enum rather than free-form event names. An open string is how an event log
/// becomes unqueryable within a year: three spellings of the same thing and no way to know which
/// mattered. Adding a kind is a considered change, which is the point.
/// </summary>
public enum ToolEventKind
{
    // --- Server-owned. Recorded by the API itself, never accepted from a client (see below). ---

    GarmentCreated,

    /// <summary>The engine contradicted the seller's own date claim. THE metric of the thesis.</summary>
    DatingFlagRaised,

    // --- Client-reported ---

    ListingDrafted,

    /// <summary>Carries the elapsed draft-to-publish time, which is "time per listing".</summary>
    ListingPublished,

    MeasurementProposed,

    /// <summary>Taken as offered — the glance-and-accept case the measurement tool exists for.</summary>
    MeasurementAccepted,

    /// <summary>Accepted after the seller dragged a point. Counts against acceptance, not as a failure.</summary>
    MeasurementAdjusted,

    /// <summary>Thrown away and measured by hand. The failure case.</summary>
    MeasurementRejected,

    ExtensionPublishAttempted,
    ExtensionPublishSucceeded,

    /// <summary>Carries the platform and a short reason code. The honest denominator of "does the
    /// extension actually work", which is the number a seller would most want before installing.</summary>
    ExtensionPublishFailed,

    /// <summary>The seller agreed the flag was right — they had the date wrong.</summary>
    DatingFlagUpheld,

    /// <summary>The seller says the flag was wrong. Just as important: it is how we find bad rules.</summary>
    DatingFlagDismissed,
}

/// <summary>
/// One recorded event. Per-seller and joinable to a garment, because the questions worth asking are
/// per-seller ("are ten of them using it weekly?") and per-garment ("was the flagged piece actually
/// misdated?") — a page-view-shaped counter cannot answer either.
///
/// Carries no seller-authored text. Everything here is an enum, an id, a duration or a short code, so
/// the log stays free of listing content and of anything that would need redacting later.
/// </summary>
public class ToolEvent : ToolBaseEntity
{
    /// <summary>The authenticated user the event belongs to. Always taken from the caller's identity,
    /// never from the request body — otherwise any seller could write events as any other.</summary>
    public Guid SellerId { get; set; }

    public ToolEventKind Kind { get; set; }

    /// <summary>Which marketplace, for the extension and publish events. Null where it doesn't apply.</summary>
    public string? Platform { get; set; }

    /// <summary>The garment this concerns, where there is one. Not a foreign key on purpose: a garment
    /// may be deleted, and losing the history of what the tool did would defeat the point of keeping it.</summary>
    public Guid? GarmentId { get; set; }

    /// <summary>Elapsed time, for the events that measure one (draft → publish).</summary>
    public int? DurationMs { get; set; }

    /// <summary>A short machine code — a failure reason, a rule's SpecId. Never prose.</summary>
    public string? Detail { get; set; }

    /// <summary>
    /// When it actually happened, which is not when we heard about it. The extension buffers while the
    /// seller is offline, so <see cref="ToolBaseEntity.CreatedAtUtc"/> (received) and this (occurred)
    /// genuinely differ, and every rate here would be wrong if they were conflated.
    /// </summary>
    public DateTime OccurredAtUtc { get; set; }
}

/// <summary>Persisted dating rule (brief §3.4 — rules are data). Projects to the engine's
/// <see cref="DatingRule"/>. Editable/addable without shipping the engine.</summary>
public class StoredRule
{
    public string Id { get; set; } = "";

    /// <summary>The rule's id in Teodora's research document, e.g. "CARE-04".</summary>
    public string SpecId { get; set; } = "";

    public string Feature { get; set; } = "";

    /// <summary>How the rule matches: on the feature code, or against the observation's raw value.</summary>
    public MatchKind Match { get; set; } = MatchKind.Feature;

    /// <summary>Operand for the value-matching kinds. Ignored for feature matching.</summary>
    public string? Pattern { get; set; }

    public EvidenceType Type { get; set; }
    public int? NotBefore { get; set; }
    public int? NotAfter { get; set; }
    public BoundStrength Strength { get; set; }
    public int TransitionLagMonths { get; set; }
    public string? SourceCitation { get; set; }

    /// <summary>What the claim rests on — orthogonal to Strength. See <see cref="ProvenanceClass"/>.</summary>
    public ProvenanceClass Provenance { get; set; } = ProvenanceClass.CommunityConsensus;

    /// <summary>Optional transition-group membership, e.g. "CARE-1986".</summary>
    public string? TransitionGroup { get; set; }

    public RuleStatus Status { get; set; } = RuleStatus.Unverified;

    /// <summary>Open questions, conflicting sources — for the researcher, never used by the engine.</summary>
    public string? ResearchNotes { get; set; }

    public DatingRule ToDomain() => new()
    {
        Id = Id,
        SpecId = SpecId,
        Feature = Feature,
        Match = Match,
        Pattern = Pattern,
        Type = Type,
        NotBefore = NotBefore,
        NotAfter = NotAfter,
        Strength = Strength,
        TransitionLagMonths = TransitionLagMonths,
        SourceCitation = SourceCitation,
        Provenance = Provenance,
        TransitionGroup = TransitionGroup,
        Status = Status,
    };
}

/// <summary>Persisted <see cref="TransitionGroup"/> (rules doc §0.4).</summary>
public class StoredTransitionGroup
{
    public string Code { get; set; } = "";
    public string Description { get; set; } = "";
    public int PeriodStart { get; set; }
    public int PeriodEnd { get; set; }
    public int TransitionLagMonths { get; set; }
    public string? SourceCitation { get; set; }
    public ProvenanceClass Provenance { get; set; } = ProvenanceClass.CommunityConsensus;
    public RuleStatus Status { get; set; } = RuleStatus.Unverified;

    public TransitionGroup ToDomain() => new()
    {
        Code = Code,
        Description = Description,
        PeriodStart = PeriodStart,
        PeriodEnd = PeriodEnd,
        TransitionLagMonths = TransitionLagMonths,
        SourceCitation = SourceCitation,
        Provenance = Provenance,
        Status = Status,
    };
}
