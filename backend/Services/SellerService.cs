using System.Text.RegularExpressions;
using Eden_Relics_BE.Auth;
using Eden_Relics_BE.Data.Entities;
using Eden_Relics_BE.DTOs;
using Eden_Relics_BE.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Eden_Relics_BE.Services;

public partial class SellerService(
    IRepository<Seller> sellers,
    IRepository<User> users,
    IRepository<Product> products,
    IStripeConnectService connect) : ISellerService
{
    public async Task<SellerDto> ApplyAsync(Guid userId, SellerApplicationDto dto)
    {
        // One seller per user — return the existing application rather than duplicating.
        Seller? existing = await sellers.Query().FirstOrDefaultAsync(s => s.OwnerUserId == userId);
        if (existing is not null)
        {
            return Map(existing);
        }

        Seller seller = new()
        {
            OwnerUserId = userId,
            BusinessName = dto.BusinessName.Trim(),
            Slug = await UniqueSlugAsync(string.IsNullOrWhiteSpace(dto.Slug) ? dto.BusinessName : dto.Slug),
            Bio = string.IsNullOrWhiteSpace(dto.Bio) ? null : dto.Bio.Trim(),
            ContactEmail = string.IsNullOrWhiteSpace(dto.ContactEmail) ? null : dto.ContactEmail.Trim().ToLowerInvariant(),
            LogoUrl = string.IsNullOrWhiteSpace(dto.LogoUrl) ? null : dto.LogoUrl.Trim(),
            ApprovalStatus = SellerApprovalStatus.Applied,
            IsHouse = false,
        };
        await sellers.AddAsync(seller);
        return Map(seller);
    }

    public async Task<SellerDto?> GetMineAsync(Guid userId)
    {
        Seller? seller = await sellers.Query().FirstOrDefaultAsync(s => s.OwnerUserId == userId);
        return seller is null ? null : Map(seller);
    }

    public async Task<SellerDto?> GetPublicBySlugAsync(string slug)
    {
        string normalised = slug.Trim().ToLowerInvariant();
        Seller? seller = await sellers.Query()
            .FirstOrDefaultAsync(s => s.Slug == normalised && s.ApprovalStatus == SellerApprovalStatus.Approved);
        return seller is null ? null : Map(seller);
    }

    public async Task<IReadOnlyList<SellerProductCardDto>> GetPublicProductsAsync(string slug)
    {
        string normalised = slug.Trim().ToLowerInvariant();
        Seller? seller = await sellers.Query()
            .FirstOrDefaultAsync(s => s.Slug == normalised && s.ApprovalStatus == SellerApprovalStatus.Approved);
        if (seller is null)
        {
            return [];
        }
        List<Product> rows = await products.Query()
            .Where(p => p.SellerId == seller.Id && p.Status == ProductStatus.Live)
            .OrderByDescending(p => p.CreatedAtUtc)
            .ToListAsync();
        return rows.Select(p => new SellerProductCardDto(
            p.Id, p.Name, p.Slug, p.Price, p.SalePrice, p.ImageUrl, p.Era, p.Category, p.Size, p.Condition)).ToList();
    }

    public async Task<IReadOnlyList<SellerDto>> ListAsync(SellerApprovalStatus? status)
    {
        IQueryable<Seller> query = sellers.Query();
        if (status is not null)
        {
            query = query.Where(s => s.ApprovalStatus == status);
        }
        List<Seller> rows = await query.OrderByDescending(s => s.CreatedAtUtc).ToListAsync();
        return rows.Select(Map).ToList();
    }

    public async Task<SellerDto?> SetStatusAsync(Guid sellerId, SellerApprovalStatus status, string? note)
    {
        Seller? seller = await sellers.GetByIdAsync(sellerId);
        if (seller is null || seller.IsHouse)
        {
            return null;
        }

        seller.ApprovalStatus = status;
        await sellers.UpdateAsync(seller);

        // Approving a seller grants the owning user the Seller role so they can reach the seller
        // dashboard. (Their existing JWT keeps the old role until it expires or they re-login.)
        if (status == SellerApprovalStatus.Approved && seller.OwnerUserId is Guid ownerId)
        {
            User? owner = await users.GetByIdAsync(ownerId);
            if (owner is not null && owner.Role != Roles.Admin && owner.Role != Roles.Seller)
            {
                owner.Role = Roles.Seller;
                await users.UpdateAsync(owner);
            }
        }

        return Map(seller);
    }

    public async Task<string?> StartConnectOnboardingAsync(Guid userId, string returnUrl, string refreshUrl)
    {
        Seller? seller = await sellers.Query().FirstOrDefaultAsync(s => s.OwnerUserId == userId);
        if (seller is null || seller.ApprovalStatus != SellerApprovalStatus.Approved)
        {
            return null;
        }
        if (string.IsNullOrEmpty(seller.StripeConnectedAccountId))
        {
            seller.StripeConnectedAccountId = await connect.CreateAccountAsync(seller.ContactEmail);
            await sellers.UpdateAsync(seller);
        }
        return await connect.CreateAccountLinkAsync(seller.StripeConnectedAccountId, returnUrl, refreshUrl);
    }

    public async Task<bool> RefreshConnectStatusAsync(Guid userId)
    {
        Seller? seller = await sellers.Query().FirstOrDefaultAsync(s => s.OwnerUserId == userId);
        if (seller?.StripeConnectedAccountId is null)
        {
            return false;
        }
        (bool charges, bool payouts) = await connect.GetAccountStatusAsync(seller.StripeConnectedAccountId);
        bool complete = charges && payouts;
        if (complete != seller.ConnectOnboardingComplete)
        {
            seller.ConnectOnboardingComplete = complete;
            await sellers.UpdateAsync(seller);
        }
        return complete;
    }

    private async Task<string> UniqueSlugAsync(string source)
    {
        string baseSlug = Slugify(source);
        if (baseSlug.Length == 0)
        {
            baseSlug = "seller";
        }
        string slug = baseSlug;
        int suffix = 2;
        while (await sellers.Query().AnyAsync(s => s.Slug == slug))
        {
            slug = $"{baseSlug}-{suffix++}";
        }
        return slug;
    }

    private static string Slugify(string input)
    {
        string lowered = input.Trim().ToLowerInvariant();
        return NonSlugChars().Replace(lowered, "-").Trim('-');
    }

    [GeneratedRegex(@"[^a-z0-9]+")]
    private static partial Regex NonSlugChars();

    private static SellerDto Map(Seller s) => new(
        s.Id,
        s.BusinessName,
        s.Slug,
        s.Bio,
        s.LogoUrl,
        s.ContactEmail,
        s.ApprovalStatus.ToString(),
        s.IsHouse,
        s.ConnectOnboardingComplete,
        s.CreatedAtUtc);
}
