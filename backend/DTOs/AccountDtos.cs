using System.ComponentModel.DataAnnotations;

namespace Eden_Relics_BE.DTOs;

public record UpdateProfileDto(
    [Required, MaxLength(100)] string FirstName,
    [Required, MaxLength(100)] string LastName
);

public record AddressDto(
    [MaxLength(200)] string? AddressLine1,
    [MaxLength(200)] string? AddressLine2,
    [MaxLength(100)] string? City,
    [MaxLength(100)] string? County,
    [MaxLength(20)] string? Postcode,
    [MaxLength(100)] string? Country
);

public record ChangePasswordDto(
    [Required] string CurrentPassword,
    [Required, MinLength(8), MaxLength(128)] string NewPassword
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
