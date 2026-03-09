namespace Eden_Relics_BE.Data.Entities;

public class User : BaseEntity
{
    public required string Email { get; set; }
    public required string PasswordHash { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public string Role { get; set; } = "Customer";
    public string? ExternalProvider { get; set; }
    public string? ExternalProviderId { get; set; }
    public bool MfaEnabled { get; set; }
    public string? MfaSecret { get; set; }
    public string? PasswordResetToken { get; set; }
    public DateTime? PasswordResetTokenExpiresUtc { get; set; }
    public bool EmailVerified { get; set; }
    public string? EmailVerificationToken { get; set; }

    // Delivery address
    public string? DeliveryAddressLine1 { get; set; }
    public string? DeliveryAddressLine2 { get; set; }
    public string? DeliveryCity { get; set; }
    public string? DeliveryCounty { get; set; }
    public string? DeliveryPostcode { get; set; }
    public string? DeliveryCountry { get; set; }

    // Billing address
    public string? BillingAddressLine1 { get; set; }
    public string? BillingAddressLine2 { get; set; }
    public string? BillingCity { get; set; }
    public string? BillingCounty { get; set; }
    public string? BillingPostcode { get; set; }
    public string? BillingCountry { get; set; }

    // Payment display info (not actual card numbers)
    public string? PaymentCardholderName { get; set; }
    public string? PaymentCardLast4 { get; set; }
    public string? PaymentCardBrand { get; set; }
    public int? PaymentCardExpiryMonth { get; set; }
    public int? PaymentCardExpiryYear { get; set; }
}
