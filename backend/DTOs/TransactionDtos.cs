namespace Eden_Relics_BE.DTOs;

public record CreateTransactionDto(
    DateTime Date,
    string Description,
    decimal Amount,
    string Category,
    string? Platform,
    string? Reference,
    string? Notes
);

public record UpdateTransactionDto(
    DateTime? Date,
    string? Description,
    decimal? Amount,
    string? Category,
    string? Platform,
    string? Reference,
    string? ReceiptUrl,
    string? Notes
);

public record TransactionDto(
    Guid Id,
    DateTime Date,
    string Description,
    decimal Amount,
    string Category,
    string? Platform,
    string? Reference,
    string? ReceiptUrl,
    string? Notes,
    DateTime CreatedAtUtc
);
