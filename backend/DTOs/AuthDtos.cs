namespace Eden_Relics_BE.DTOs;

public record RegisterDto(
    string Email,
    string Password,
    string FirstName,
    string LastName
);

public record LoginDto(
    string Email,
    string Password
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
    string Email
);

public record ResetPasswordDto(
    string Email,
    string Token,
    string NewPassword
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
