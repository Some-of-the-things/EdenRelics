using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Eden_Relics_BE.Data;
using Eden_Relics_BE.Data.Entities;
using Eden_Relics_BE.DTOs;
using Eden_Relics_BE.Repositories;
using Fido2NetLib;
using Fido2NetLib.Objects;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.IdentityModel.Tokens;

namespace Eden_Relics_BE.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PasskeyController : ControllerBase
{
    private readonly IFido2 _fido2;
    private readonly EdenRelicsDbContext _db;
    private readonly IRepository<User> _userRepository;
    private readonly IConfiguration _configuration;
    private readonly IDistributedCache _cache;

    public PasskeyController(
        IFido2 fido2,
        EdenRelicsDbContext db,
        IRepository<User> userRepository,
        IConfiguration configuration,
        IDistributedCache cache)
    {
        _fido2 = fido2;
        _db = db;
        _userRepository = userRepository;
        _configuration = configuration;
        _cache = cache;
    }

    // --- Registration (requires auth) ---

    [HttpPost("register-options")]
    [Authorize]
    public async Task<ActionResult> RegisterOptions()
    {
        User? user = await GetCurrentUser();
        if (user is null) return Unauthorized();

        Fido2User fido2User = new()
        {
            Id = user.Id.ToByteArray(),
            Name = user.Email,
            DisplayName = $"{user.FirstName} {user.LastName}"
        };

        List<UserCredential> existing = await _db.UserCredentials
            .Where(c => c.UserId == user.Id)
            .ToListAsync();

        List<PublicKeyCredentialDescriptor> excludeCredentials = existing
            .Select(c => new PublicKeyCredentialDescriptor(c.CredentialId))
            .ToList();

        CredentialCreateOptions options = _fido2.RequestNewCredential(
            new RequestNewCredentialParams
            {
                User = fido2User,
                ExcludeCredentials = excludeCredentials,
                AuthenticatorSelection = new AuthenticatorSelection
                {
                    ResidentKey = ResidentKeyRequirement.Preferred,
                    UserVerification = UserVerificationRequirement.Preferred
                },
                AttestationPreference = AttestationConveyancePreference.None
            });

        string optionsJson = options.ToJson();
        await _cache.SetStringAsync($"fido2_reg_{user.Id}", optionsJson,
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) });

        return Content(optionsJson, "application/json");
    }

    [HttpPost("register")]
    [Authorize]
    public async Task<ActionResult> Register([FromBody] AuthenticatorAttestationRawResponse attestation)
    {
        User? user = await GetCurrentUser();
        if (user is null) return Unauthorized();

        string? optionsJson = await _cache.GetStringAsync($"fido2_reg_{user.Id}");
        if (optionsJson is null)
            return BadRequest(new { message = "Registration session expired." });

        CredentialCreateOptions options = CredentialCreateOptions.FromJson(optionsJson);

        RegisteredPublicKeyCredential result = await _fido2.MakeNewCredentialAsync(
            new MakeNewCredentialParams
            {
                AttestationResponse = attestation,
                OriginalOptions = options,
                IsCredentialIdUniqueToUserCallback = async (args, _) =>
                {
                    byte[] credId = args.CredentialId;
                    bool exists = await _db.UserCredentials.AnyAsync(c => c.CredentialId == credId);
                    return !exists;
                }
            });

        UserCredential credential = new()
        {
            UserId = user.Id,
            CredentialId = result.Id,
            PublicKey = result.PublicKey,
            UserHandle = user.Id.ToByteArray(),
            SignatureCounter = result.SignCount,
            CredType = result.Type.ToString(),
            AaGuid = Guid.Empty
        };

        _db.UserCredentials.Add(credential);
        await _db.SaveChangesAsync();
        await _cache.RemoveAsync($"fido2_reg_{user.Id}");

        return Ok(new { message = "Passkey registered successfully." });
    }

    // --- Authentication (public) ---

    [HttpPost("login-options")]
    public async Task<ActionResult> LoginOptions([FromBody] PasskeyLoginOptionsDto? dto)
    {
        List<PublicKeyCredentialDescriptor> allowedCredentials = [];

        if (!string.IsNullOrWhiteSpace(dto?.Email))
        {
            IEnumerable<User> users = await _userRepository.FindAsync(u => u.Email == dto.Email.ToLowerInvariant());
            User? user = users.FirstOrDefault();
            if (user is not null)
            {
                List<UserCredential> creds = await _db.UserCredentials
                    .Where(c => c.UserId == user.Id)
                    .ToListAsync();
                allowedCredentials = creds
                    .Select(c => new PublicKeyCredentialDescriptor(c.CredentialId))
                    .ToList();
            }
        }

        AssertionOptions options = _fido2.GetAssertionOptions(
            new GetAssertionOptionsParams
            {
                AllowedCredentials = allowedCredentials,
                UserVerification = UserVerificationRequirement.Preferred
            });

        string optionsJson = options.ToJson();
        string sessionId = Guid.NewGuid().ToString();
        await _cache.SetStringAsync($"fido2_auth_{sessionId}", optionsJson,
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) });

        return Content(
            System.Text.Json.JsonSerializer.Serialize(new { sessionId, options = System.Text.Json.JsonDocument.Parse(optionsJson).RootElement }),
            "application/json");
    }

    [HttpPost("login")]
    public async Task<ActionResult> Login([FromBody] PasskeyLoginDto dto)
    {
        string? optionsJson = await _cache.GetStringAsync($"fido2_auth_{dto.SessionId}");
        if (optionsJson is null)
            return BadRequest(new { message = "Login session expired." });

        AssertionOptions options = AssertionOptions.FromJson(optionsJson);

        byte[] credentialId = dto.Response.RawId;
        UserCredential? storedCred = await _db.UserCredentials
            .FirstOrDefaultAsync(c => c.CredentialId == credentialId);

        if (storedCred is null)
            return Unauthorized(new { message = "Passkey not recognised." });

        VerifyAssertionResult result = await _fido2.MakeAssertionAsync(
            new MakeAssertionParams
            {
                AssertionResponse = dto.Response,
                OriginalOptions = options,
                StoredPublicKey = storedCred.PublicKey,
                StoredSignatureCounter = storedCred.SignatureCounter,
                IsUserHandleOwnerOfCredentialIdCallback = async (args, _) =>
                {
                    UserCredential? cred = await _db.UserCredentials
                        .FirstOrDefaultAsync(c => c.CredentialId == args.CredentialId && c.UserHandle == args.UserHandle);
                    return cred is not null;
                }
            });

        storedCred.SignatureCounter = result.SignCount;
        await _db.SaveChangesAsync();
        await _cache.RemoveAsync($"fido2_auth_{dto.SessionId}");

        User? user = await _userRepository.GetByIdAsync(storedCred.UserId);
        if (user is null) return Unauthorized();

        string token = GenerateToken(user);
        return Ok(new AuthResponseDto(token, new UserDto(user.Id, user.Email, user.FirstName, user.LastName, user.Role, user.EmailVerified)));
    }

    // --- Management (requires auth) ---

    [HttpGet("credentials")]
    [Authorize]
    public async Task<ActionResult> GetCredentials()
    {
        User? user = await GetCurrentUser();
        if (user is null) return Unauthorized();

        List<UserCredential> creds = await _db.UserCredentials
            .Where(c => c.UserId == user.Id)
            .OrderByDescending(c => c.CreatedAtUtc)
            .ToListAsync();

        var result = creds.Select(c => new
        {
            id = c.Id,
            nickname = c.Nickname,
            createdAt = c.CreatedAtUtc
        });

        return Ok(result);
    }

    [HttpDelete("credentials/{id:guid}")]
    [Authorize]
    public async Task<ActionResult> DeleteCredential(Guid id)
    {
        User? user = await GetCurrentUser();
        if (user is null) return Unauthorized();

        UserCredential? cred = await _db.UserCredentials
            .FirstOrDefaultAsync(c => c.Id == id && c.UserId == user.Id);

        if (cred is null) return NotFound();

        cred.IsDeleted = true;
        await _db.SaveChangesAsync();

        return Ok(new { message = "Passkey removed." });
    }

    [HttpPut("credentials/{id:guid}")]
    [Authorize]
    public async Task<ActionResult> RenameCredential(Guid id, [FromBody] RenamePasskeyDto dto)
    {
        User? user = await GetCurrentUser();
        if (user is null) return Unauthorized();

        UserCredential? cred = await _db.UserCredentials
            .FirstOrDefaultAsync(c => c.Id == id && c.UserId == user.Id);

        if (cred is null) return NotFound();

        cred.Nickname = dto.Nickname;
        await _db.SaveChangesAsync();

        return Ok(new { message = "Passkey renamed." });
    }

    private async Task<User?> GetCurrentUser()
    {
        string? userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdStr is null || !Guid.TryParse(userIdStr, out Guid userId))
            return null;

        return await _userRepository.GetByIdAsync(userId);
    }

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
}

public record PasskeyLoginOptionsDto(string? Email);
public record PasskeyLoginDto(string SessionId, AuthenticatorAssertionRawResponse Response);
public record RenamePasskeyDto(string Nickname);
