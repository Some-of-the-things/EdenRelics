namespace Eden_Relics_BE.Services;

/// <summary>
/// Produces an AI baseline draft for a care entry. The output is ALWAYS treated as a draft
/// pending human expert review — it is never auto-published. Wraps the Anthropic SDK
/// (same key/config as <see cref="TranslationService"/>).
/// </summary>
public interface ICareDraftService
{
    bool IsConfigured { get; }
    Task<CareFabricDraft?> DraftFabricAsync(string name, IReadOnlyList<string> targetKeywords);
    Task<CareIssueDraft?> DraftIssueAsync(string name, IReadOnlyList<string> targetKeywords);

    /// <summary>Best-guess fabric identification from a photo. Assistive only — never authoritative.</summary>
    Task<FabricIdentifyResult?> IdentifyFabricAsync(string base64Image, string mediaType, IReadOnlyList<string> knownFabrics);
}

public record CareFabricDraft(
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
    string MetaDescription);

public record CareIssueDraft(
    string Causes,
    string GeneralMethod,
    string WhatNotToDo,
    string WhenToSeeAPro,
    string MetaTitle,
    string MetaDescription);

public record FabricIdentifyResult(List<FabricGuess> Guesses, string Note);
public record FabricGuess(string Name, double Confidence);
