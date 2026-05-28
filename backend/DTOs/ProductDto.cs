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
    DateTime CreatedAtUtc
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
    DateTime CreatedAtUtc
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
    int? BackdatePriceDays = null
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
    ProductStatus? Status
);
