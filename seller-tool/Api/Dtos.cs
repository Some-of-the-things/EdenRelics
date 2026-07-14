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
