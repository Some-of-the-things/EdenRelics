namespace Eden_Relics_BE.DTOs;

/// <summary>A seller creating one of their own one-of-one listings. It enters as Stock +
/// PendingReview (hidden from the public) until an admin approves it. Sellers don't set
/// cost/sourcing fields — those are platform concerns.</summary>
public record SellerListingCreateDto(
    string Name,
    string Description,
    decimal Price,
    string Era,
    string Category,
    string Size,
    string Condition,
    string ImageUrl,
    List<string>? AdditionalImageUrls,
    string? Material);

/// <summary>A listing as seen by its seller, the moderation queue, and admin.</summary>
public record SellerListingDto(
    Guid Id,
    string Name,
    string Slug,
    string Sku,
    decimal Price,
    string Era,
    string Category,
    string Size,
    string Condition,
    string ImageUrl,
    string Status,
    string ModerationStatus,
    string? ModerationNote,
    Guid SellerId,
    DateTime CreatedAtUtc);

/// <summary>Optional admin note on a moderation decision (reason for rejection).</summary>
public record ModerationDecisionDto(string? Note);
