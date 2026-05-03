namespace Eden_Relics_BE.DTOs;

public record OffsiteSaleDto(
    Guid Id,
    string DressName,
    string Era,
    string Category,
    string Size,
    string Condition,
    decimal SalePrice,
    decimal CostPrice,
    string Platform,
    DateTime SaleDateUtc,
    string? Notes
);

public record CreateOffsiteSaleDto(
    string DressName,
    string Era,
    string Category,
    string Size,
    string Condition,
    decimal SalePrice,
    decimal CostPrice,
    string Platform,
    DateTime SaleDateUtc,
    string? Notes
);
