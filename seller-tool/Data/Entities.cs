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

    /// <summary>Storage key for the captured label/photo (e.g. an R2 object key). The archive asset.</summary>
    public string? ImageKey { get; set; }

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

/// <summary>Persisted dating rule (brief §3.4 — rules are data). Projects to the engine's
/// <see cref="DatingRule"/>. Editable/addable without shipping the engine.</summary>
public class StoredRule
{
    public string Id { get; set; } = "";
    public string Feature { get; set; } = "";
    public EvidenceType Type { get; set; }
    public int? NotBefore { get; set; }
    public int? NotAfter { get; set; }
    public BoundStrength Strength { get; set; }
    public int TransitionLagMonths { get; set; }
    public string? SourceCitation { get; set; }
    public RuleStatus Status { get; set; } = RuleStatus.Unverified;

    public DatingRule ToDomain() => new()
    {
        Id = Id,
        Feature = Feature,
        Type = Type,
        NotBefore = NotBefore,
        NotAfter = NotAfter,
        Strength = Strength,
        TransitionLagMonths = TransitionLagMonths,
        SourceCitation = SourceCitation,
        Status = Status,
    };
}
