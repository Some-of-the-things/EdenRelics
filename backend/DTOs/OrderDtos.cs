namespace Eden_Relics_BE.DTOs;

public record CreateOrderDto(
    List<CreateOrderItemDto> Items,
    string? GuestEmail
);

public record CreateOrderItemDto(
    Guid ProductId,
    int Quantity
);

public record OrderDto(
    Guid Id,
    string Status,
    decimal Total,
    DateTime CreatedAtUtc,
    List<OrderItemDto> Items
);

public record OrderItemDto(
    Guid ProductId,
    string ProductName,
    decimal UnitPrice,
    int Quantity
);
