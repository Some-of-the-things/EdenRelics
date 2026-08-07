namespace EdenRelics.SellerTool.Api;

// --- Requests ---
public record CreateGarmentRequest(string? Title, string? SellerRef, string? Reference);

public record AddEvidenceRequest(
    string Type, string Feature, string? RawValue, string? ImageKey, string? Origin, string? Confirmation);

public record DateGarmentRequest(int? ClaimEarliest, int? ClaimLatest);

public record AddRuleRequest(
    string Id, string Feature, string? Type, int? NotBefore, int? NotAfter,
    string? Strength, int TransitionLagMonths, string? SourceCitation);

// --- Responses ---
public record EvidenceDto(Guid Id, string Type, string Feature, string? RawValue, string? ImageKey, string Origin, string Confirmation);

public record EstimateDto(Guid Id, int? Earliest, int? Latest, string Outcome, string Confirmation, DateTime ComputedAtUtc);

public record GarmentDto(
    Guid Id, string? Title, string? SellerRef, string? Reference,
    IReadOnlyList<EvidenceDto> Evidence, IReadOnlyList<EstimateDto> Estimates);

public record GarmentSummaryDto(
    Guid Id, string? Title, string? SellerRef, string? Reference, DateTime CreatedAtUtc,
    int EvidenceCount, int? LatestEarliest, int? LatestLatest, string? LatestOutcome, string? LatestConfirmation);

public record ClaimFlagDto(string Strength, string Message);

public record EvidenceChainDto(string RuleId, string Feature, string Bound, string Strength, string? Source);

public record DateResultDto(
    int? Earliest, int? Latest, string Outcome, ClaimFlagDto? ClaimFlag, IReadOnlyList<EvidenceChainDto> Evidence);

// --- Dating preview: run the engine without persisting anything ---

/// <summary>
/// One observation to feed the engine. <paramref name="RawValue"/> carries the text read off a label
/// where there is one (a phone number, an origin line); feature-matching rules ignore it.
/// </summary>
public record PreviewEvidenceRequest(string Feature, string? Type, string? RawValue);

public record DatingPreviewRequest(
    IReadOnlyList<PreviewEvidenceRequest> Evidence, int? ClaimEarliest, int? ClaimLatest);

/// <summary>
/// The full evidence chain, unlike <see cref="EvidenceChainDto"/> which the garment-dating endpoint
/// returns. Provenance, spec id and the set-aside reason are the parts that show WHY the engine
/// reached a range, so a preview that omitted them would be showing the answer without the argument.
/// </summary>
public record PreviewChainDto(
    string RuleId, string SpecId, string Feature, string Bound, string Strength,
    string Provenance, bool Applied, string? ExclusionReason, string? Source);

public record DatingPreviewDto(
    int? Earliest, int? Latest, string Outcome, string Range,
    ClaimFlagDto? ClaimFlag, IReadOnlyList<PreviewChainDto> Evidence);

/// <summary>
/// A feature the live rule set can actually act on, for the UI to offer. Derived from the rules
/// themselves so the picker cannot drift from the data — a hardcoded list would silently offer
/// features no rule matches, which is how a demo starts lying.
/// </summary>
public record DatingFeatureDto(
    string Feature, string Type, string MatchKind, IReadOnlyList<string> SpecIds,
    int? NotBefore, int? NotAfter, string Strength, bool NeedsValue);
