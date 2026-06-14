using System.Security.Claims;
using System.Text.Json;
using Eden_Relics_BE.Services;
using Fido2NetLib;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Eden_Relics_BE.Controllers;

[ApiController]
[Route("api/[controller]")]
[EnableRateLimiting("auth")]
public class PasskeyController(IPasskeyService passkeys) : ControllerBase
{
    // --- Registration (requires auth) ---

    [HttpPost("register-options")]
    [Authorize]
    public async Task<ActionResult> RegisterOptions()
    {
        if (GetUserId() is not Guid userId)
        {
            return Unauthorized();
        }

        string? optionsJson = await passkeys.BuildRegisterOptionsAsync(userId);
        return optionsJson is null ? Unauthorized() : Content(optionsJson, "application/json");
    }

    [HttpPost("register")]
    [Authorize]
    public async Task<ActionResult> Register([FromBody] AuthenticatorAttestationRawResponse attestation)
    {
        if (GetUserId() is not Guid userId)
        {
            return Unauthorized();
        }

        PasskeyRegisterOutcome outcome = await passkeys.RegisterAsync(userId, attestation);
        return outcome switch
        {
            PasskeyRegisterOutcome.Unauthorized => Unauthorized(),
            PasskeyRegisterOutcome.SessionExpired => BadRequest(new { message = "Registration session expired." }),
            _ => Ok(new { message = "Passkey registered successfully." }),
        };
    }

    // --- Authentication (public) ---

    [HttpPost("login-options")]
    public async Task<ActionResult> LoginOptions([FromBody] PasskeyLoginOptionsDto? dto)
    {
        PasskeyLoginOptionsResult result = await passkeys.BuildLoginOptionsAsync(dto?.Email);

        return Content(
            JsonSerializer.Serialize(new
            {
                sessionId = result.SessionId,
                options = JsonDocument.Parse(result.OptionsJson).RootElement
            }),
            "application/json");
    }

    [HttpPost("login")]
    public async Task<ActionResult> Login([FromBody] PasskeyLoginDto dto)
    {
        PasskeyLoginResult result = await passkeys.LoginAsync(dto);
        return result.Outcome switch
        {
            PasskeyLoginOutcome.SessionExpired => BadRequest(new { message = "Login session expired." }),
            PasskeyLoginOutcome.NotRecognised => Unauthorized(new { message = "Passkey not recognised." }),
            PasskeyLoginOutcome.UserMissing => Unauthorized(),
            PasskeyLoginOutcome.MfaRequired => Ok(new { mfaRequired = true, mfaToken = result.MfaToken }),
            _ => Ok(result.Auth),
        };
    }

    // --- Management (requires auth) ---

    [HttpGet("credentials")]
    [Authorize]
    public async Task<ActionResult> GetCredentials()
    {
        if (GetUserId() is not Guid userId)
        {
            return Unauthorized();
        }

        List<PasskeyCredentialDto>? creds = await passkeys.GetCredentialsAsync(userId);
        return creds is null ? Unauthorized() : Ok(creds);
    }

    [HttpDelete("credentials/{id:guid}")]
    [Authorize]
    public async Task<ActionResult> DeleteCredential(Guid id)
    {
        if (GetUserId() is not Guid userId)
        {
            return Unauthorized();
        }

        PasskeyManageOutcome outcome = await passkeys.DeleteCredentialAsync(userId, id);
        return outcome switch
        {
            PasskeyManageOutcome.Unauthorized => Unauthorized(),
            PasskeyManageOutcome.NotFound => NotFound(),
            _ => Ok(new { message = "Passkey removed." }),
        };
    }

    [HttpPut("credentials/{id:guid}")]
    [Authorize]
    public async Task<ActionResult> RenameCredential(Guid id, [FromBody] RenamePasskeyDto dto)
    {
        if (GetUserId() is not Guid userId)
        {
            return Unauthorized();
        }

        PasskeyManageOutcome outcome = await passkeys.RenameCredentialAsync(userId, id, dto.Nickname);
        return outcome switch
        {
            PasskeyManageOutcome.Unauthorized => Unauthorized(),
            PasskeyManageOutcome.NotFound => NotFound(),
            _ => Ok(new { message = "Passkey renamed." }),
        };
    }

    private Guid? GetUserId()
    {
        string? userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userIdStr, out Guid userId) ? userId : null;
    }
}
