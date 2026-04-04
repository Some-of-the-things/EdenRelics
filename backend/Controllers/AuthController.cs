using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Eden_Relics_BE.Data.Entities;
using Eden_Relics_BE.DTOs;
using Eden_Relics_BE.Repositories;
using Eden_Relics_BE.Services;
using Google.Apis.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using OtpNet;

namespace Eden_Relics_BE.Controllers;

[ApiController]
[Route("api/[controller]")]
[EnableRateLimiting("auth")]
public class AuthController : ControllerBase
{
    private readonly IRepository<User> _userRepository;
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IEmailService _emailService;
    private readonly PasswordHasher<User> _passwordHasher = new();

    private readonly IWebHostEnvironment _environment;

    public AuthController(IRepository<User> userRepository, IConfiguration configuration, IHttpClientFactory httpClientFactory, IEmailService emailService, IWebHostEnvironment environment)
    {
        _userRepository = userRepository;
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
        _emailService = emailService;
        _environment = environment;
    }

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponseDto>> Register(RegisterDto dto)
    {
        IEnumerable<User> existing = await _userRepository.FindAsync(u => u.Email == dto.Email);
        if (existing.Any())
        {
            return Conflict(new { message = "Email already registered." });
        }

        string verificationToken = GenerateSecureToken();

        User user = new()
        {
            Email = dto.Email.ToLowerInvariant(),
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            PasswordHash = string.Empty,
            EmailVerificationToken = verificationToken
        };
        user.PasswordHash = _passwordHasher.HashPassword(user, dto.Password);

        await _userRepository.AddAsync(user);

        _ = _emailService.SendVerificationEmailAsync(user.Email, user.FirstName, verificationToken);

        string token = GenerateToken(user);
        return Ok(new AuthResponseDto(token, ToDto(user)));
    }

    [HttpPost("login")]
    public async Task<ActionResult> Login(LoginDto dto)
    {
        IEnumerable<User> users = await _userRepository.FindAsync(u => u.Email == dto.Email.ToLowerInvariant());
        User? user = users.FirstOrDefault();

        if (user is null)
        {
            return Unauthorized(new { message = "Invalid email or password." });
        }

        PasswordVerificationResult result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, dto.Password);
        if (result == PasswordVerificationResult.Failed)
        {
            return Unauthorized(new { message = "Invalid email or password." });
        }

        if (user.MfaEnabled)
        {
            string mfaToken = GenerateMfaToken(user);
            return Ok(new { mfaRequired = true, mfaToken });
        }

        string token = GenerateToken(user);
        return Ok(new AuthResponseDto(token, ToDto(user)));
    }

    [HttpPost("mfa-verify")]
    public async Task<ActionResult<AuthResponseDto>> MfaVerify(MfaLoginDto dto)
    {
        Guid? userId = ValidateMfaToken(dto.MfaToken);
        if (userId is null)
        {
            return Unauthorized(new { message = "Invalid or expired MFA session." });
        }

        User? user = await _userRepository.GetByIdAsync(userId.Value);
        if (user is null || !user.MfaEnabled || user.MfaSecret is null)
        {
            return Unauthorized(new { message = "Invalid MFA session." });
        }

        Totp totp = new(Base32Encoding.ToBytes(user.MfaSecret));
        bool valid = totp.VerifyTotp(dto.Code, out _, new VerificationWindow(previous: 1, future: 1));

        if (!valid)
        {
            return BadRequest(new { message = "Invalid code. Please try again." });
        }

        string token = GenerateToken(user);
        return Ok(new AuthResponseDto(token, ToDto(user)));
    }

    [HttpPost("forgot-password")]
    [EnableRateLimiting("contact")]
    public async Task<ActionResult> ForgotPassword(ForgotPasswordDto dto)
    {
        IEnumerable<User> users = await _userRepository.FindAsync(u => u.Email == dto.Email.ToLowerInvariant());
        User? user = users.FirstOrDefault();

        if (user is null)
        {
            return Ok(new { message = "If that email exists, a reset link has been sent." });
        }

        string resetToken = GenerateSecureToken();
        user.PasswordResetToken = resetToken;
        user.PasswordResetTokenExpiresUtc = DateTime.UtcNow.AddHours(1);
        await _userRepository.UpdateAsync(user);

        _ = _emailService.SendPasswordResetEmailAsync(user.Email, user.FirstName, resetToken);

        return Ok(new { message = "If that email exists, a reset link has been sent." });
    }

    [HttpPost("reset-password")]
    public async Task<ActionResult> ResetPassword(ResetPasswordDto dto)
    {
        IEnumerable<User> users = await _userRepository.FindAsync(u => u.Email == dto.Email.ToLowerInvariant());
        User? user = users.FirstOrDefault();

        if (user is null
            || user.PasswordResetToken != dto.Token
            || user.PasswordResetTokenExpiresUtc < DateTime.UtcNow)
        {
            return BadRequest(new { message = "Invalid or expired reset token." });
        }

        user.PasswordHash = _passwordHasher.HashPassword(user, dto.NewPassword);
        user.PasswordResetToken = null;
        user.PasswordResetTokenExpiresUtc = null;
        await _userRepository.UpdateAsync(user);

        return Ok(new { message = "Password has been reset successfully." });
    }

    [HttpPost("verify-email")]
    public async Task<ActionResult> VerifyEmail(VerifyEmailDto dto)
    {
        IEnumerable<User> users = await _userRepository.FindAsync(u => u.Email == dto.Email.ToLowerInvariant());
        User? user = users.FirstOrDefault();

        if (user is null
            || user.EmailVerificationToken != dto.Token)
        {
            return BadRequest(new { message = "Invalid verification token." });
        }

        user.EmailVerified = true;
        user.EmailVerificationToken = null;
        await _userRepository.UpdateAsync(user);

        return Ok(new { message = "Email verified successfully." });
    }

    [HttpPost("resend-verification")]
    [Authorize]
    public async Task<ActionResult> ResendVerification()
    {
        string? userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdStr is null || !Guid.TryParse(userIdStr, out Guid userId))
        {
            return Unauthorized();
        }

        User? user = await _userRepository.GetByIdAsync(userId);
        if (user is null)
        {
            return Unauthorized();
        }

        if (user.EmailVerified)
        {
            return BadRequest(new { message = "Email is already verified." });
        }

        string verificationToken = GenerateSecureToken();

        user.EmailVerificationToken = verificationToken;
        await _userRepository.UpdateAsync(user);

        _ = _emailService.SendVerificationEmailAsync(user.Email, user.FirstName, verificationToken);

        return Ok(new { message = "Verification email has been sent." });
    }

    [HttpPost("external-login")]
    public async Task<ActionResult<AuthResponseDto>> ExternalLogin(ExternalLoginDto dto)
    {
        ExternalUserInfo? info = dto.Provider.ToLowerInvariant() switch
        {
            "google" => await VerifyGoogleToken(dto.IdToken),
            "facebook" => await VerifyFacebookToken(dto.IdToken),
            "apple" => await VerifyAppleToken(dto.IdToken),
            _ => null
        };

        if (info is null)
        {
            return Unauthorized(new { message = "Invalid external login." });
        }

        // Look for existing user by provider ID
        IEnumerable<User> byProvider = await _userRepository.FindAsync(
            u => u.ExternalProvider == dto.Provider && u.ExternalProviderId == info.ProviderId);
        User? user = byProvider.FirstOrDefault();

        // If not found, look by email — only link if the existing account's email is verified
        if (user is null)
        {
            IEnumerable<User> byEmail = await _userRepository.FindAsync(
                u => u.Email == info.Email.ToLowerInvariant());
            user = byEmail.FirstOrDefault();

            if (user is not null)
            {
                if (!user.EmailVerified)
                {
                    return BadRequest(new { message = "An account with this email exists but is not verified. Please verify your email first or log in with your password." });
                }
                user.ExternalProvider = dto.Provider;
                user.ExternalProviderId = info.ProviderId;
                await _userRepository.UpdateAsync(user);
            }
        }

        // If still not found, create a new account
        if (user is null)
        {
            user = new User
            {
                Email = info.Email.ToLowerInvariant(),
                FirstName = info.FirstName ?? "",
                LastName = info.LastName ?? "",
                PasswordHash = string.Empty,
                ExternalProvider = dto.Provider,
                ExternalProviderId = info.ProviderId,
                EmailVerified = true
            };
            await _userRepository.AddAsync(user);
        }

        string token = GenerateToken(user);
        return Ok(new AuthResponseDto(token, ToDto(user)));
    }

    private async Task<ExternalUserInfo?> VerifyGoogleToken(string idToken)
    {
        try
        {
            string? clientId = _configuration["OAuth:Google:ClientId"];
            GoogleJsonWebSignature.ValidationSettings settings = new()
            {
                Audience = [clientId!]
            };
            GoogleJsonWebSignature.Payload payload = await GoogleJsonWebSignature.ValidateAsync(idToken, settings);
            return new ExternalUserInfo(payload.Subject, payload.Email, payload.GivenName, payload.FamilyName);
        }
        catch
        {
            return null;
        }
    }

    private async Task<ExternalUserInfo?> VerifyFacebookToken(string accessToken)
    {
        try
        {
            HttpClient client = _httpClientFactory.CreateClient();
            string url = $"https://graph.facebook.com/me?fields=id,email,first_name,last_name&access_token={Uri.EscapeDataString(accessToken)}";
            HttpResponseMessage response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            string json = await response.Content.ReadAsStringAsync();
            using JsonDocument doc = JsonDocument.Parse(json);
            JsonElement root = doc.RootElement;

            string? id = root.GetProperty("id").GetString();
            string? email = root.TryGetProperty("email", out JsonElement emailEl) ? emailEl.GetString() : null;
            string? firstName = root.TryGetProperty("first_name", out JsonElement fnEl) ? fnEl.GetString() : null;
            string? lastName = root.TryGetProperty("last_name", out JsonElement lnEl) ? lnEl.GetString() : null;

            if (id is null || email is null)
            {
                return null;
            }
            return new ExternalUserInfo(id, email, firstName, lastName);
        }
        catch
        {
            return null;
        }
    }

    private async Task<ExternalUserInfo?> VerifyAppleToken(string idToken)
    {
        try
        {
            JwtSecurityTokenHandler handler = new();
            JwtSecurityToken jwt = handler.ReadJwtToken(idToken);

            // Fetch Apple's public keys
            HttpClient client = _httpClientFactory.CreateClient();
            string keysJson = await client.GetStringAsync("https://appleid.apple.com/auth/keys");

            Microsoft.IdentityModel.Tokens.JsonWebKeySet jwks = new(keysJson);

            TokenValidationParameters parameters = new()
            {
                ValidateIssuer = true,
                ValidIssuer = "https://appleid.apple.com",
                ValidateAudience = true,
                ValidAudience = _configuration["OAuth:Apple:ClientId"],
                ValidateLifetime = true,
                IssuerSigningKeys = jwks.GetSigningKeys()
            };

            handler.ValidateToken(idToken, parameters, out _);

            string? sub = jwt.Claims.FirstOrDefault(c => c.Type == "sub")?.Value;
            string? email = jwt.Claims.FirstOrDefault(c => c.Type == "email")?.Value;

            if (sub is null || email is null)
            {
                return null;
            }
            return new ExternalUserInfo(sub, email, null, null);
        }
        catch
        {
            return null;
        }
    }

    private record ExternalUserInfo(string ProviderId, string Email, string? FirstName, string? LastName);

    private string GenerateToken(User user)
    {
        string key = _configuration["Jwt:Key"]!;
        SymmetricSecurityKey securityKey = new(Encoding.UTF8.GetBytes(key));
        SigningCredentials credentials = new(securityKey, SecurityAlgorithms.HmacSha256);

        Claim[] claims =
        [
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role),
            new(ClaimTypes.GivenName, user.FirstName)
        ];

        JwtSecurityToken token = new(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddDays(7),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private string GenerateMfaToken(User user)
    {
        string key = _configuration["Jwt:Key"]!;
        SymmetricSecurityKey securityKey = new(Encoding.UTF8.GetBytes(key));
        SigningCredentials credentials = new(securityKey, SecurityAlgorithms.HmacSha256);

        Claim[] claims =
        [
            new("mfa_user_id", user.Id.ToString()),
            new("purpose", "mfa")
        ];

        JwtSecurityToken token = new(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private Guid? ValidateMfaToken(string mfaToken)
    {
        try
        {
            string key = _configuration["Jwt:Key"]!;
            TokenValidationParameters parameters = new()
            {
                ValidateIssuer = true,
                ValidIssuer = _configuration["Jwt:Issuer"],
                ValidateAudience = true,
                ValidAudience = _configuration["Jwt:Audience"],
                ValidateLifetime = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key))
            };

            JwtSecurityTokenHandler handler = new();
            ClaimsPrincipal principal = handler.ValidateToken(mfaToken, parameters, out _);

            string? purpose = principal.FindFirstValue("purpose");
            string? userIdStr = principal.FindFirstValue("mfa_user_id");

            if (purpose != "mfa" || userIdStr is null || !Guid.TryParse(userIdStr, out Guid userId))
            {
                return null;
            }

            return userId;
        }
        catch
        {
            return null;
        }
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResponseDto>> Refresh()
    {
        // Extract the token manually to allow expired tokens
        string? authHeader = Request.Headers.Authorization.FirstOrDefault();
        if (authHeader is null || !authHeader.StartsWith("Bearer "))
        {
            return Unauthorized();
        }

        string expiredToken = authHeader["Bearer ".Length..];

        try
        {
            string key = _configuration["Jwt:Key"]!;
            TokenValidationParameters parameters = new()
            {
                ValidateIssuer = true,
                ValidIssuer = _configuration["Jwt:Issuer"],
                ValidateAudience = true,
                ValidAudience = _configuration["Jwt:Audience"],
                ValidateLifetime = false, // Allow expired tokens
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key))
            };

            JwtSecurityTokenHandler handler = new();
            ClaimsPrincipal principal = handler.ValidateToken(expiredToken, parameters, out SecurityToken validatedToken);

            // Only allow refresh within 30 days of expiry
            if (validatedToken.ValidTo < DateTime.UtcNow.AddDays(-30))
            {
                return Unauthorized(new { message = "Token too old to refresh." });
            }

            string? userIdStr = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdStr is null || !Guid.TryParse(userIdStr, out Guid userId))
            {
                return Unauthorized();
            }

            User? user = await _userRepository.GetByIdAsync(userId);
            if (user is null)
            {
                return Unauthorized();
            }

            string newToken = GenerateToken(user);
            return Ok(new AuthResponseDto(newToken, ToDto(user)));
        }
        catch
        {
            return Unauthorized();
        }
    }

    [HttpPost("promote-admin")]
    [Authorize]
    public async Task<ActionResult<AuthResponseDto>> PromoteToAdmin()
    {
        if (!_environment.IsDevelopment())
        {
            return NotFound();
        }

        string userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        IEnumerable<User> users = await _userRepository.FindAsync(u => u.Id == Guid.Parse(userId));
        User? user = users.FirstOrDefault();
        if (user is null) { return NotFound(); }

        user.Role = "Admin";
        await _userRepository.UpdateAsync(user);

        string token = GenerateToken(user);
        return Ok(new AuthResponseDto(token, ToDto(user)));
    }

    private static string GenerateSecureToken()
    {
        byte[] tokenBytes = new byte[32];
        RandomNumberGenerator.Fill(tokenBytes);
        return Convert.ToBase64String(tokenBytes).Replace("/", "_").Replace("+", "-").TrimEnd('=');
    }

    private static UserDto ToDto(User u) => new(u.Id, u.Email, u.FirstName, u.LastName, u.Role, u.EmailVerified);
}
