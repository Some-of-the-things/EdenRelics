namespace Eden_Relics_BE.DTOs;

public record UpdateProfileDto(
    string FirstName,
    string LastName
);

public record AddressDto(
    string? AddressLine1,
    string? AddressLine2,
    string? City,
    string? County,
    string? Postcode,
    string? Country
);

public record ChangePasswordDto(
    string CurrentPassword,
    string NewPassword
);

public record UpdatePaymentDto(
    string? CardholderName,
    string? CardLast4,
    string? CardBrand,
    int? ExpiryMonth,
    int? ExpiryYear
);

public record AccountProfileDto(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    AddressDto DeliveryAddress,
    AddressDto BillingAddress,
    PaymentInfoDto? Payment,
    bool MfaEnabled,
    bool EmailVerified
);

public record MfaSetupDto(
    string Secret,
    string QrUri
);

public record VerifyMfaDto(
    string Code
);

public record PaymentInfoDto(
    string? CardholderName,
    string? CardLast4,
    string? CardBrand,
    int? ExpiryMonth,
    int? ExpiryYear
);
