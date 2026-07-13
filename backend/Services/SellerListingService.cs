using System.Text.RegularExpressions;
using Eden_Relics_BE.Data.Entities;
using Eden_Relics_BE.DTOs;
using Eden_Relics_BE.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Eden_Relics_BE.Services;

public partial class SellerListingService(
    IRepository<Product> products,
    IRepository<Seller> sellers) : ISellerListingService
{
    public async Task<SellerListingDto?> CreateAsync(Guid userId, SellerListingCreateDto dto)
    {
        Seller? seller = await sellers.Query().FirstOrDefaultAsync(s => s.OwnerUserId == userId);
        if (seller is null || seller.ApprovalStatus != SellerApprovalStatus.Approved)
        {
            return null;
        }

        // SKUs are unique per seller; check the seller's own space (incl. soft-deleted so numbers
        // aren't reused). Slugs are globally unique across all products.
        List<string> sellerSkus = await products.Query(includeDeleted: true)
            .Where(p => p.SellerId == seller.Id)
            .Select(p => p.Sku)
            .ToListAsync();

        Product product = new()
        {
            SellerId = seller.Id,
            Name = dto.Name.Trim(),
            Slug = await UniqueSlugAsync(dto.Name),
            Sku = SkuGenerator.Next(sellerSkus),
            Description = dto.Description,
            Price = dto.Price,
            Era = dto.Era,
            Category = dto.Category,
            Size = dto.Size,
            Condition = dto.Condition,
            Material = string.IsNullOrWhiteSpace(dto.Material) ? null : dto.Material.Trim(),
            ImageUrl = dto.ImageUrl,
            AdditionalImageUrls = dto.AdditionalImageUrls ?? [],
            Status = ProductStatus.Stock,                          // hidden from the public site
            ModerationStatus = ProductModerationStatus.PendingReview,
            PriceSetAtUtc = DateTime.UtcNow,
        };
        await products.AddAsync(product);
        return Map(product);
    }

    public async Task<IReadOnlyList<SellerListingDto>> ListMineAsync(Guid userId)
    {
        Seller? seller = await sellers.Query().FirstOrDefaultAsync(s => s.OwnerUserId == userId);
        if (seller is null)
        {
            return [];
        }
        List<Product> rows = await products.Query()
            .Where(p => p.SellerId == seller.Id)
            .OrderByDescending(p => p.CreatedAtUtc)
            .ToListAsync();
        return rows.Select(Map).ToList();
    }

    public async Task<IReadOnlyList<SellerListingDto>> ListForModerationAsync(ProductModerationStatus status)
    {
        List<Product> rows = await products.Query()
            .Where(p => p.ModerationStatus == status)
            .OrderBy(p => p.CreatedAtUtc)
            .ToListAsync();
        return rows.Select(Map).ToList();
    }

    public async Task<SellerListingDto?> ApproveAsync(Guid productId)
    {
        Product? product = await products.GetByIdAsync(productId);
        if (product is null || product.ModerationStatus != ProductModerationStatus.PendingReview)
        {
            return null;
        }
        product.Status = ProductStatus.Live;
        product.ModerationStatus = ProductModerationStatus.Approved;
        product.ModerationNote = null;
        await products.UpdateAsync(product);
        return Map(product);
    }

    public async Task<SellerListingDto?> RejectAsync(Guid productId, string? note)
    {
        Product? product = await products.GetByIdAsync(productId);
        if (product is null || product.ModerationStatus != ProductModerationStatus.PendingReview)
        {
            return null;
        }
        // Stays Status=Stock so it remains hidden from the public site.
        product.ModerationStatus = ProductModerationStatus.Rejected;
        product.ModerationNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        await products.UpdateAsync(product);
        return Map(product);
    }

    private async Task<string> UniqueSlugAsync(string source)
    {
        string baseSlug = Slugify(source);
        if (baseSlug.Length == 0)
        {
            baseSlug = "listing";
        }
        string slug = baseSlug;
        int suffix = 2;
        while (await products.Query(includeDeleted: true).AnyAsync(p => p.Slug == slug))
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

    private static SellerListingDto Map(Product p) => new(
        p.Id,
        p.Name,
        p.Slug,
        p.Sku,
        p.Price,
        p.Era,
        p.Category,
        p.Size,
        p.Condition,
        p.ImageUrl,
        p.Status.ToString(),
        p.ModerationStatus.ToString(),
        p.ModerationNote,
        p.SellerId,
        p.CreatedAtUtc);
}
