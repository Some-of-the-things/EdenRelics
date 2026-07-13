namespace Eden_Relics_BE.DTOs;

/// <summary>A logged-in user's application to become a seller on the curated hub.</summary>
public record SellerApplicationDto(
    string BusinessName,
    string? Slug,
    string? Bio,
    string? ContactEmail,
    string? LogoUrl);

/// <summary>Seller as returned to the public profile, the seller's own dashboard, and admin.</summary>
public record SellerDto(
    Guid Id,
    string BusinessName,
    string Slug,
    string? Bio,
    string? LogoUrl,
    string? ContactEmail,
    string ApprovalStatus,
    bool IsHouse,
    bool ConnectOnboardingComplete,
    DateTime CreatedAtUtc);

/// <summary>Admin note attached to a reject/suspend decision (optional).</summary>
public record SellerStatusUpdateDto(string? Note);
