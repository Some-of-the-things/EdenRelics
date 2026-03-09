namespace Eden_Relics_BE.DTOs;

public record ProductDto(
    Guid Id,
    string Name,
    string Description,
    decimal Price,
    string Era,
    string Category,
    string Size,
    string Condition,
    string ImageUrl,
    bool InStock
);

public record CreateProductDto(
    string Name,
    string Description,
    decimal Price,
    string Era,
    string Category,
    string Size,
    string Condition,
    string ImageUrl,
    bool InStock
);

public record UpdateProductDto(
    string? Name,
    string? Description,
    decimal? Price,
    string? Era,
    string? Category,
    string? Size,
    string? Condition,
    string? ImageUrl,
    bool? InStock
);
