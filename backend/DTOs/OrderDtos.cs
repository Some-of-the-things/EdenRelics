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

public record CheckoutResponseDto(
    Guid OrderId,
    string CheckoutUrl
);

public record AdminOrderDto(
    Guid Id,
    string Status,
    decimal Total,
    DateTime CreatedAtUtc,
    string CustomerEmail,
    string? CustomerName,
    List<OrderItemDto> Items
);

public record UpdateOrderStatusDto(
    string Status
);
