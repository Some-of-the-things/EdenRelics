using System.ComponentModel.DataAnnotations;

namespace Eden_Relics_BE.DTOs;

public record RegisterDto(
    [Required, EmailAddress, MaxLength(256)] string Email,
    [Required, MinLength(8), MaxLength(128)] string Password,
    [Required, MaxLength(100)] string FirstName,
    [Required, MaxLength(100)] string LastName
);

public record LoginDto(
    [Required, EmailAddress, MaxLength(256)] string Email,
    [Required] string Password
);

public record AuthResponseDto(
    string Token,
    UserDto User
);

public record UserDto(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    string Role,
    bool EmailVerified
);

public record ForgotPasswordDto(
    [Required, EmailAddress, MaxLength(256)] string Email
);

public record ResetPasswordDto(
    [Required, EmailAddress, MaxLength(256)] string Email,
    [Required] string Token,
    [Required, MinLength(8), MaxLength(128)] string NewPassword
);

public record ExternalLoginDto(
    string Provider,
    string IdToken
);

public record MfaLoginDto(
    string MfaToken,
    string Code
);

public record VerifyEmailDto(
    string Email,
    string Token
);
