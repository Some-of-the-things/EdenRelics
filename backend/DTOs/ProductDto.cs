namespace Eden_Relics_BE.DTOs;

public record ProductDto(
    Guid Id,
    string Name,
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
    bool InStock
);

public record ProductAdminDto(
    Guid Id,
    string Name,
    string Description,
    decimal Price,
    decimal? SalePrice,
    decimal CostPrice,
    string? Supplier,
    string Era,
    string Category,
    string Size,
    string Condition,
    string ImageUrl,
    List<string> AdditionalImageUrls,
    List<string> VideoUrls,
    bool InStock,
    int ViewCount
);

public record CreateProductDto(
    string Name,
    string Description,
    decimal Price,
    decimal? SalePrice,
    decimal CostPrice,
    string? Supplier,
    string Era,
    string Category,
    string Size,
    string Condition,
    string ImageUrl,
    List<string>? AdditionalImageUrls,
    List<string>? VideoUrls,
    bool InStock
);

public record UpdateProductDto(
    string? Name,
    string? Description,
    decimal? Price,
    decimal? SalePrice,
    decimal? CostPrice,
    string? Supplier,
    string? Era,
    string? Category,
    string? Size,
    string? Condition,
    string? ImageUrl,
    List<string>? AdditionalImageUrls,
    List<string>? VideoUrls,
    bool? InStock
);
