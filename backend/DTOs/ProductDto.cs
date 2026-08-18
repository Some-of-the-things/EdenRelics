using Eden_Relics_BE.Data.Entities;

namespace Eden_Relics_BE.DTOs;

public record ProductDto(
    Guid Id,
    string Name,
    string Slug,
    string Description,
    decimal Price,
    decimal? SalePrice,
    bool ShowReduction,
    int DiscountPercent,
    string Era,
    string Category,
    string Size,
    string Condition,
    string ImageUrl,
    List<string> AdditionalImageUrls,
    List<string> VideoUrls,
    bool InStock,
    DateTime CreatedAtUtc,
    string? Material = null
);

public record ProductAdminDto(
    Guid Id,
    string Name,
    string Slug,
    string Sku,
    string Description,
    decimal Price,
    decimal? SalePrice,
    decimal CostPrice,
    DateTime? StockPurchaseDate,
    string? Supplier,
    string Era,
    string Category,
    string Size,
    string Condition,
    string ImageUrl,
    List<string> AdditionalImageUrls,
    List<string> VideoUrls,
    bool InStock,
    ProductStatus Status,
    int ViewCount,
    DateTime CreatedAtUtc,
    string? Material = null
);

public record CreateProductDto(
    string Name,
    string Description,
    decimal Price,
    decimal? SalePrice,
    decimal CostPrice,
    DateTime? StockPurchaseDate,
    string? Supplier,
    string Era,
    string Category,
    string Size,
    string Condition,
    string ImageUrl,
    List<string>? AdditionalImageUrls,
    List<string>? VideoUrls,
    bool InStock,
    ProductStatus? Status = null,
    string? Sku = null,
    int? BackdatePriceDays = null,
    string? Material = null
);

/// <summary>
/// Bulk "x% off" across a hand-picked set of products. The discount is applied to each
/// product's own Price to derive its SalePrice — Price itself is never touched, so the
/// 28-day reduction-rule history (PriceSetAtUtc) stays honest.
/// </summary>
public record BulkSalePriceDto(
    List<Guid> ProductIds,
    decimal DiscountPercent,
    bool NotifyFavourites = false
);

public record BulkClearSaleDto(List<Guid> ProductIds);

/// <summary>
/// Outcome of a bulk sale-price change. Skipped counts selected products that were left alone
/// (unknown id, no price to discount, or the result wouldn't be a reduction); Notified counts
/// products whose favourite alerts were queued, not individual emails (sending is
/// fire-and-forget); Products carries the rows back so the admin UI can patch its list.
/// </summary>
public record BulkSaleResultDto(
    int Updated,
    int Skipped,
    int Notified,
    List<ProductAdminDto> Products
);

public record UpdateProductDto(
    string? Name,
    string? Slug,
    string? Sku,
    string? Description,
    decimal? Price,
    decimal? SalePrice,
    decimal? CostPrice,
    DateTime? StockPurchaseDate,
    string? Supplier,
    string? Era,
    string? Category,
    string? Size,
    string? Condition,
    string? ImageUrl,
    List<string>? AdditionalImageUrls,
    List<string>? VideoUrls,
    bool? InStock,
    ProductStatus? Status,
    string? Material = null
);
