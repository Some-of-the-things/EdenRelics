using System.Security.Claims;
using Eden_Relics_BE.Data.Entities;
using Eden_Relics_BE.DTOs;
using Eden_Relics_BE.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using OtpNet;

namespace Eden_Relics_BE.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AccountController : ControllerBase
{
    private readonly IRepository<User> _userRepository;
    private readonly PasswordHasher<User> _passwordHasher = new();

    public AccountController(IRepository<User> userRepository)
    {
        _userRepository = userRepository;
    }

    [HttpGet("profile")]
    public async Task<ActionResult<AccountProfileDto>> GetProfile()
    {
        User? user = await GetCurrentUser();
        if (user is null)
        {
            return Unauthorized();
        }

        return Ok(ToProfileDto(user));
    }

    [HttpPut("profile")]
    public async Task<ActionResult<AccountProfileDto>> UpdateProfile(UpdateProfileDto dto)
    {
        User? user = await GetCurrentUser();
        if (user is null)
        {
            return Unauthorized();
        }

        user.FirstName = dto.FirstName;
        user.LastName = dto.LastName;
        await _userRepository.UpdateAsync(user);

        return Ok(ToProfileDto(user));
    }

    [HttpPut("delivery-address")]
    public async Task<ActionResult<AccountProfileDto>> UpdateDeliveryAddress(AddressDto dto)
    {
        User? user = await GetCurrentUser();
        if (user is null)
        {
            return Unauthorized();
        }

        user.DeliveryAddressLine1 = dto.AddressLine1;
        user.DeliveryAddressLine2 = dto.AddressLine2;
        user.DeliveryCity = dto.City;
        user.DeliveryCounty = dto.County;
        user.DeliveryPostcode = dto.Postcode;
        user.DeliveryCountry = dto.Country;
        await _userRepository.UpdateAsync(user);

        return Ok(ToProfileDto(user));
    }

    [HttpPut("billing-address")]
    public async Task<ActionResult<AccountProfileDto>> UpdateBillingAddress(AddressDto dto)
    {
        User? user = await GetCurrentUser();
        if (user is null)
        {
            return Unauthorized();
        }

        user.BillingAddressLine1 = dto.AddressLine1;
        user.BillingAddressLine2 = dto.AddressLine2;
        user.BillingCity = dto.City;
        user.BillingCounty = dto.County;
        user.BillingPostcode = dto.Postcode;
        user.BillingCountry = dto.Country;
        await _userRepository.UpdateAsync(user);

        return Ok(ToProfileDto(user));
    }

    [HttpPut("payment")]
    public async Task<ActionResult<AccountProfileDto>> UpdatePayment(UpdatePaymentDto dto)
    {
        User? user = await GetCurrentUser();
        if (user is null)
        {
            return Unauthorized();
        }

        user.PaymentCardholderName = dto.CardholderName;
        user.PaymentCardLast4 = dto.CardLast4;
        user.PaymentCardBrand = dto.CardBrand;
        user.PaymentCardExpiryMonth = dto.ExpiryMonth;
        user.PaymentCardExpiryYear = dto.ExpiryYear;
        await _userRepository.UpdateAsync(user);

        return Ok(ToProfileDto(user));
    }

    [HttpPost("change-password")]
    public async Task<ActionResult> ChangePassword(ChangePasswordDto dto)
    {
        User? user = await GetCurrentUser();
        if (user is null)
        {
            return Unauthorized();
        }

        PasswordVerificationResult result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, dto.CurrentPassword);
        if (result == PasswordVerificationResult.Failed)
        {
            return BadRequest(new { message = "Current password is incorrect." });
        }

        user.PasswordHash = _passwordHasher.HashPassword(user, dto.NewPassword);
        await _userRepository.UpdateAsync(user);

        return Ok(new { message = "Password changed successfully." });
    }

    [HttpPost("mfa/setup")]
    public async Task<ActionResult<MfaSetupDto>> SetupMfa()
    {
        User? user = await GetCurrentUser();
        if (user is null)
        {
            return Unauthorized();
        }

        if (user.MfaEnabled)
        {
            return BadRequest(new { message = "MFA is already enabled." });
        }

        byte[] secret = KeyGeneration.GenerateRandomKey(20);
        string base32Secret = Base32Encoding.ToString(secret);

        user.MfaSecret = base32Secret;
        await _userRepository.UpdateAsync(user);

        string qrUri = $"otpauth://totp/Eden%20Relics:{Uri.EscapeDataString(user.Email)}?secret={base32Secret}&issuer=Eden%20Relics&digits=6&period=30";

        return Ok(new MfaSetupDto(base32Secret, qrUri));
    }

    [HttpPost("mfa/verify")]
    public async Task<ActionResult> VerifyMfa(VerifyMfaDto dto)
    {
        User? user = await GetCurrentUser();
        if (user is null)
        {
            return Unauthorized();
        }

        if (user.MfaSecret is null)
        {
            return BadRequest(new { message = "MFA setup has not been started." });
        }

        Totp totp = new(Base32Encoding.ToBytes(user.MfaSecret));
        bool valid = totp.VerifyTotp(dto.Code, out _, new VerificationWindow(previous: 1, future: 1));

        if (!valid)
        {
            return BadRequest(new { message = "Invalid code. Please try again." });
        }

        user.MfaEnabled = true;
        await _userRepository.UpdateAsync(user);

        return Ok(new { message = "MFA has been enabled successfully." });
    }

    [HttpPost("mfa/disable")]
    public async Task<ActionResult> DisableMfa(VerifyMfaDto dto)
    {
        User? user = await GetCurrentUser();
        if (user is null)
        {
            return Unauthorized();
        }

        if (!user.MfaEnabled || user.MfaSecret is null)
        {
            return BadRequest(new { message = "MFA is not enabled." });
        }

        Totp totp = new(Base32Encoding.ToBytes(user.MfaSecret));
        bool valid = totp.VerifyTotp(dto.Code, out _, new VerificationWindow(previous: 1, future: 1));

        if (!valid)
        {
            return BadRequest(new { message = "Invalid code." });
        }

        user.MfaEnabled = false;
        user.MfaSecret = null;
        await _userRepository.UpdateAsync(user);

        return Ok(new { message = "MFA has been disabled." });
    }

    [HttpGet("export-data")]
    public async Task<ActionResult<object>> ExportData()
    {
        User? user = await GetCurrentUser();
        if (user is null)
        {
            return Unauthorized();
        }

        var data = new
        {
            Profile = new
            {
                user.Email,
                user.FirstName,
                user.LastName,
                user.EmailVerified,
                AccountCreated = user.CreatedAtUtc,
            },
            DeliveryAddress = new
            {
                user.DeliveryAddressLine1,
                user.DeliveryAddressLine2,
                user.DeliveryCity,
                user.DeliveryCounty,
                user.DeliveryPostcode,
                user.DeliveryCountry,
            },
            BillingAddress = new
            {
                user.BillingAddressLine1,
                user.BillingAddressLine2,
                user.BillingCity,
                user.BillingCounty,
                user.BillingPostcode,
                user.BillingCountry,
            },
            Payment = user.PaymentCardLast4 is not null ? new
            {
                user.PaymentCardholderName,
                user.PaymentCardLast4,
                user.PaymentCardBrand,
                user.PaymentCardExpiryMonth,
                user.PaymentCardExpiryYear,
            } : null,
            SecuritySettings = new
            {
                MfaEnabled = user.MfaEnabled,
            },
        };

        return Ok(data);
    }

    [HttpDelete("delete-account")]
    public async Task<ActionResult> DeleteAccount()
    {
        User? user = await GetCurrentUser();
        if (user is null)
        {
            return Unauthorized();
        }

        await _userRepository.DeleteAsync(user.Id);

        return Ok(new { message = "Your account and all associated data have been permanently deleted." });
    }

    private async Task<User?> GetCurrentUser()
    {
        string? userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdStr is null || !Guid.TryParse(userIdStr, out Guid userId))
        {
            return null;
        }

        return await _userRepository.GetByIdAsync(userId);
    }

    private static AccountProfileDto ToProfileDto(User u) => new(
        u.Id,
        u.Email,
        u.FirstName,
        u.LastName,
        new AddressDto(
            u.DeliveryAddressLine1,
            u.DeliveryAddressLine2,
            u.DeliveryCity,
            u.DeliveryCounty,
            u.DeliveryPostcode,
            u.DeliveryCountry
        ),
        new AddressDto(
            u.BillingAddressLine1,
            u.BillingAddressLine2,
            u.BillingCity,
            u.BillingCounty,
            u.BillingPostcode,
            u.BillingCountry
        ),
        u.PaymentCardLast4 is not null
            ? new PaymentInfoDto(
                u.PaymentCardholderName,
                u.PaymentCardLast4,
                u.PaymentCardBrand,
                u.PaymentCardExpiryMonth,
                u.PaymentCardExpiryYear
            )
            : null,
        u.MfaEnabled,
        u.EmailVerified
    );
}
