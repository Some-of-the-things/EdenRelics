using Eden_Relics_BE.DTOs;
using Fido2NetLib;

namespace Eden_Relics_BE.Services;

public interface IPasskeyService
{
    /// <summary>Builds + caches WebAuthn registration options. Null if the user no longer exists.</summary>
    Task<string?> BuildRegisterOptionsAsync(Guid userId);
    Task<PasskeyRegisterOutcome> RegisterAsync(Guid userId, AuthenticatorAttestationRawResponse attestation);

    Task<PasskeyLoginOptionsResult> BuildLoginOptionsAsync(string? email);
    Task<PasskeyLoginResult> LoginAsync(PasskeyLoginDto dto);

    /// <summary>Lists the user's passkeys. Null if the user no longer exists (→ Unauthorized).</summary>
    Task<List<PasskeyCredentialDto>?> GetCredentialsAsync(Guid userId);
    Task<PasskeyManageOutcome> DeleteCredentialAsync(Guid userId, Guid credentialId);
    Task<PasskeyManageOutcome> RenameCredentialAsync(Guid userId, Guid credentialId, string nickname);
}

public enum PasskeyRegisterOutcome { Unauthorized, SessionExpired, Success }
public enum PasskeyLoginOutcome { SessionExpired, NotRecognised, UserMissing, MfaRequired, Success }
public enum PasskeyManageOutcome { Unauthorized, NotFound, Success }

public record PasskeyLoginOptionsResult(string SessionId, string OptionsJson);
public record PasskeyLoginResult(PasskeyLoginOutcome Outcome, string? MfaToken, AuthResponseDto? Auth);
public record PasskeyCredentialDto(Guid Id, string? Nickname, DateTime CreatedAt);

public record PasskeyLoginOptionsDto(string? Email);
public record PasskeyLoginDto(string SessionId, AuthenticatorAssertionRawResponse Response);
public record RenamePasskeyDto(string Nickname);
