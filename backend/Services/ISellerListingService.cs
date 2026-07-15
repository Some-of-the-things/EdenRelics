using Eden_Relics_BE.Data.Entities;
using Eden_Relics_BE.DTOs;

namespace Eden_Relics_BE.Services;

public interface ISellerListingService
{
    /// <summary>An approved seller creates a listing. Enters Stock + PendingReview (hidden until
    /// approved). Returns null if the user isn't an approved seller.</summary>
    Task<SellerListingDto?> CreateAsync(Guid userId, SellerListingCreateDto dto);

    /// <summary>The current seller's own listings (dashboard).</summary>
    Task<IReadOnlyList<SellerListingDto>> ListMineAsync(Guid userId);

    /// <summary>Admin moderation queue, filtered by moderation status (default PendingReview).</summary>
    Task<IReadOnlyList<SellerListingDto>> ListForModerationAsync(ProductModerationStatus status);

    /// <summary>Admin approves a pending listing: Stock+PendingReview -> Live+Approved (now public).
    /// Returns null if not found or not currently pending.</summary>
    Task<SellerListingDto?> ApproveAsync(Guid productId);

    /// <summary>Admin rejects a pending listing: -> Rejected (stays Stock/hidden), with a note.
    /// Returns null if not found or not currently pending.</summary>
    Task<SellerListingDto?> RejectAsync(Guid productId, string? note);
}
