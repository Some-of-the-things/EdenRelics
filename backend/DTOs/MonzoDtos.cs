namespace Eden_Relics_BE.DTOs;

public record MonzoTransactionDto(
    Guid Id,
    string MonzoId,
    DateTime Date,
    string Description,
    decimal Amount,
    string Currency,
    string Category,
    string? MerchantName,
    string? MerchantLogo,
    string? Notes,
    string? Tags,
    bool IsLoad,
    string? DeclineReason,
    DateTime? SettledAt,
    string? UserCategory,
    string? Platform,
    string? ReceiptUrl,
    DateTime CreatedAtUtc
);

public record MonzoAnnotateDto(
    string? Notes,
    string? Tags,
    string? UserCategory,
    string? Platform
);

public record MonzoBalanceDto(
    decimal Balance,
    decimal TotalBalance,
    string Currency,
    decimal SpendToday
);

public record MonzoConnectionStatusDto(
    bool Connected,
    string? AccountId
);

public record MonzoCallbackDto(
    string Code,
    string State
);
