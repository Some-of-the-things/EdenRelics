namespace Eden_Relics_BE.Services;

public interface ICareService
{
    /// <summary>Whether AI draft generation is configured (Anthropic key present).</summary>
    bool AiDraftingAvailable { get; }

    // --- Admin worklist ---
    Task<List<CareWorklistItemDto>> GetWorklistAsync();

    // --- Fabric (admin) ---
    Task<CareFabricDto?> GetFabricAsync(Guid id);
    Task<CareFabricDto> CreateFabricAsync(SaveCareFabricDto dto);
    Task<CareFabricDto?> UpdateFabricAsync(Guid id, SaveCareFabricDto dto);
    Task<CareFabricDto?> SetFabricPublishedAsync(Guid id, bool published, string reviewedBy);
    Task<CareFabricDto?> GenerateFabricDraftAsync(Guid id);

    // --- Issue (admin) ---
    Task<CareIssueDto?> GetIssueAsync(Guid id);
    Task<CareIssueDto> CreateIssueAsync(SaveCareIssueDto dto);
    Task<CareIssueDto?> UpdateIssueAsync(Guid id, SaveCareIssueDto dto);
    Task<CareIssueDto?> SetIssuePublishedAsync(Guid id, bool published, string reviewedBy);
    Task<CareIssueDto?> GenerateIssueDraftAsync(Guid id);

    // --- Public (published only) ---
    Task<CareFabricDto?> GetPublishedFabricAsync(string slug);
    Task<CareIssueDto?> GetPublishedIssueAsync(string slug);
    Task<CareIndexDto> GetPublishedIndexAsync();
}

public record CareIndexDto(List<CareIndexItemDto> Fabrics, List<CareIndexItemDto> Issues);
public record CareIndexItemDto(string Name, string Slug, string Summary);

/// <summary>One row in the reviewer's worklist — enough to triage outstanding actions at a glance.</summary>
public record CareWorklistItemDto(
    Guid Id,
    string Type,              // "fabric" | "issue"
    string Name,
    string Slug,
    string Status,           // Draft | AiDrafted | ExpertApproved
    bool IsPublished,
    bool NeedsAction,        // true while not yet published (draft/awaiting review)
    List<string> TargetKeywords,
    string ReviewNotes,
    DateTime? LastReviewedUtc,
    DateTime UpdatedAtUtc);

public record CareFabricDto(
    Guid Id,
    string Slug,
    string Name,
    List<string> AlsoKnownAs,
    List<string> TargetKeywords,
    string Intro,
    string FiberContent,
    string HowToIdentify,
    string Washing,
    string Drying,
    string Ironing,
    string Storing,
    string VintageCautions,
    List<string> Dos,
    List<string> Donts,
    string MetaTitle,
    string MetaDescription,
    string Status,
    string ReviewNotes,
    string? ReviewedBy,
    DateTime? LastReviewedUtc,
    bool IsPublished,
    DateTime UpdatedAtUtc);

public record SaveCareFabricDto(
    string? Slug,
    string Name,
    List<string>? AlsoKnownAs,
    List<string>? TargetKeywords,
    string? Intro,
    string? FiberContent,
    string? HowToIdentify,
    string? Washing,
    string? Drying,
    string? Ironing,
    string? Storing,
    string? VintageCautions,
    List<string>? Dos,
    List<string>? Donts,
    string? MetaTitle,
    string? MetaDescription,
    string? ReviewNotes);

public record CareIssueDto(
    Guid Id,
    string Slug,
    string Name,
    List<string> AlsoKnownAs,
    List<string> TargetKeywords,
    string Causes,
    string GeneralMethod,
    string WhatNotToDo,
    string WhenToSeeAPro,
    string MetaTitle,
    string MetaDescription,
    string Status,
    string ReviewNotes,
    string? ReviewedBy,
    DateTime? LastReviewedUtc,
    bool IsPublished,
    DateTime UpdatedAtUtc);

public record SaveCareIssueDto(
    string? Slug,
    string Name,
    List<string>? AlsoKnownAs,
    List<string>? TargetKeywords,
    string? Causes,
    string? GeneralMethod,
    string? WhatNotToDo,
    string? WhenToSeeAPro,
    string? MetaTitle,
    string? MetaDescription,
    string? ReviewNotes);
