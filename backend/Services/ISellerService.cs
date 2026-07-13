using Eden_Relics_BE.Data.Entities;
using Eden_Relics_BE.DTOs;

namespace Eden_Relics_BE.Services;

public interface ISellerService
{
    /// <summary>A logged-in user applies to become a seller. Idempotent: returns the existing
    /// seller if the user already has one. New applications start as <see cref="SellerApprovalStatus.Applied"/>.</summary>
    Task<SellerDto> ApplyAsync(Guid userId, SellerApplicationDto dto);

    /// <summary>The seller owned by this user (for the seller dashboard), or null.</summary>
    Task<SellerDto?> GetMineAsync(Guid userId);

    /// <summary>Public profile lookup by slug — approved sellers only.</summary>
    Task<SellerDto?> GetPublicBySlugAsync(string slug);

    /// <summary>Admin roster, optionally filtered by approval status, newest first.</summary>
    Task<IReadOnlyList<SellerDto>> ListAsync(SellerApprovalStatus? status);

    /// <summary>Admin moderation: set a seller's approval status. Approving also grants the owner
    /// the Seller role. The house seller cannot be moderated. Returns null if not found / is house.</summary>
    Task<SellerDto?> SetStatusAsync(Guid sellerId, SellerApprovalStatus status, string? note);
}
