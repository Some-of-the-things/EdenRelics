using Eden_Relics_BE.Data.Entities;
using Eden_Relics_BE.DTOs;
using Eden_Relics_BE.Repositories;
using Fido2NetLib;
using Fido2NetLib.Objects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;

namespace Eden_Relics_BE.Services;

public class PasskeyService(
    IFido2 fido2,
    IRepository<User> users,
    IRepository<UserCredential> credentials,
    IDistributedCache cache,
    JwtTokenService tokenService) : IPasskeyService
{
    // --- Registration ---

    public async Task<string?> BuildRegisterOptionsAsync(Guid userId)
    {
        User? user = await users.GetByIdAsync(userId);
        if (user is null)
        {
            return null;
        }

        Fido2User fido2User = new()
        {
            Id = user.Id.ToByteArray(),
            Name = user.Email,
            DisplayName = $"{user.FirstName} {user.LastName}"
        };

        List<UserCredential> existing = await credentials.Query()
            .Where(c => c.UserId == user.Id)
            .ToListAsync();

        List<PublicKeyCredentialDescriptor> excludeCredentials = existing
            .Select(c => new PublicKeyCredentialDescriptor(c.CredentialId))
            .ToList();

        CredentialCreateOptions options = fido2.RequestNewCredential(
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
        await cache.SetStringAsync($"fido2_reg_{user.Id}", optionsJson,
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) });

        return optionsJson;
    }

    public async Task<PasskeyRegisterOutcome> RegisterAsync(Guid userId, AuthenticatorAttestationRawResponse attestation)
    {
        User? user = await users.GetByIdAsync(userId);
        if (user is null)
        {
            return PasskeyRegisterOutcome.Unauthorized;
        }

        string? optionsJson = await cache.GetStringAsync($"fido2_reg_{user.Id}");
        if (optionsJson is null)
        {
            return PasskeyRegisterOutcome.SessionExpired;
        }

        CredentialCreateOptions options = CredentialCreateOptions.FromJson(optionsJson);

        RegisteredPublicKeyCredential result = await fido2.MakeNewCredentialAsync(
            new MakeNewCredentialParams
            {
                AttestationResponse = attestation,
                OriginalOptions = options,
                IsCredentialIdUniqueToUserCallback = async (args, _) =>
                {
                    byte[] credId = args.CredentialId;
                    bool exists = await credentials.Query().AnyAsync(c => c.CredentialId == credId);
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

        await credentials.AddAsync(credential);
        await cache.RemoveAsync($"fido2_reg_{user.Id}");

        return PasskeyRegisterOutcome.Success;
    }

    // --- Authentication ---

    public async Task<PasskeyLoginOptionsResult> BuildLoginOptionsAsync(string? email)
    {
        List<PublicKeyCredentialDescriptor> allowedCredentials = [];

        if (!string.IsNullOrWhiteSpace(email))
        {
            IEnumerable<User> matched = await users.FindAsync(u => u.Email == email.ToLowerInvariant());
            User? user = matched.FirstOrDefault();
            if (user is not null)
            {
                List<UserCredential> creds = await credentials.Query()
                    .Where(c => c.UserId == user.Id)
                    .ToListAsync();
                allowedCredentials = creds
                    .Select(c => new PublicKeyCredentialDescriptor(c.CredentialId))
                    .ToList();
            }
        }

        AssertionOptions options = fido2.GetAssertionOptions(
            new GetAssertionOptionsParams
            {
                AllowedCredentials = allowedCredentials,
                UserVerification = UserVerificationRequirement.Preferred
            });

        string optionsJson = options.ToJson();
        string sessionId = Guid.NewGuid().ToString();
        await cache.SetStringAsync($"fido2_auth_{sessionId}", optionsJson,
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) });

        return new PasskeyLoginOptionsResult(sessionId, optionsJson);
    }

    public async Task<PasskeyLoginResult> LoginAsync(PasskeyLoginDto dto)
    {
        string? optionsJson = await cache.GetStringAsync($"fido2_auth_{dto.SessionId}");
        if (optionsJson is null)
        {
            return new PasskeyLoginResult(PasskeyLoginOutcome.SessionExpired, null, null);
        }

        AssertionOptions options = AssertionOptions.FromJson(optionsJson);

        byte[] credentialId = dto.Response.RawId;
        UserCredential? storedCred = await credentials.Query()
            .FirstOrDefaultAsync(c => c.CredentialId == credentialId);

        if (storedCred is null)
        {
            return new PasskeyLoginResult(PasskeyLoginOutcome.NotRecognised, null, null);
        }

        VerifyAssertionResult result = await fido2.MakeAssertionAsync(
            new MakeAssertionParams
            {
                AssertionResponse = dto.Response,
                OriginalOptions = options,
                StoredPublicKey = storedCred.PublicKey,
                StoredSignatureCounter = storedCred.SignatureCounter,
                IsUserHandleOwnerOfCredentialIdCallback = async (args, _) =>
                {
                    UserCredential? cred = await credentials.Query()
                        .FirstOrDefaultAsync(c => c.CredentialId == args.CredentialId && c.UserHandle == args.UserHandle);
                    return cred is not null;
                }
            });

        storedCred.SignatureCounter = result.SignCount;
        await credentials.UpdateAsync(storedCred);
        await cache.RemoveAsync($"fido2_auth_{dto.SessionId}");

        User? user = await users.GetByIdAsync(storedCred.UserId);
        if (user is null)
        {
            return new PasskeyLoginResult(PasskeyLoginOutcome.UserMissing, null, null);
        }

        // A passkey assertion is a single factor. If the account has MFA enabled, hand back
        // the same TOTP challenge the password path uses (AuthController.Login) rather than a
        // full session token, so the second factor can't be bypassed via the passkey flow.
        if (user.MfaEnabled)
        {
            string mfaToken = tokenService.GenerateMfaToken(user);
            return new PasskeyLoginResult(PasskeyLoginOutcome.MfaRequired, mfaToken, null);
        }

        string token = tokenService.GenerateToken(user);
        AuthResponseDto auth = new(token, new UserDto(user.Id, user.Email, user.FirstName, user.LastName, user.Role, user.EmailVerified));
        return new PasskeyLoginResult(PasskeyLoginOutcome.Success, null, auth);
    }

    // --- Management ---

    public async Task<List<PasskeyCredentialDto>?> GetCredentialsAsync(Guid userId)
    {
        User? user = await users.GetByIdAsync(userId);
        if (user is null)
        {
            return null;
        }

        List<UserCredential> creds = await credentials.Query()
            .Where(c => c.UserId == user.Id)
            .OrderByDescending(c => c.CreatedAtUtc)
            .ToListAsync();

        return creds.Select(c => new PasskeyCredentialDto(c.Id, c.Nickname, c.CreatedAtUtc)).ToList();
    }

    public async Task<PasskeyManageOutcome> DeleteCredentialAsync(Guid userId, Guid credentialId)
    {
        User? user = await users.GetByIdAsync(userId);
        if (user is null)
        {
            return PasskeyManageOutcome.Unauthorized;
        }

        UserCredential? cred = await credentials.Query()
            .FirstOrDefaultAsync(c => c.Id == credentialId && c.UserId == user.Id);

        if (cred is null)
        {
            return PasskeyManageOutcome.NotFound;
        }

        await credentials.DeleteAsync(cred.Id);
        return PasskeyManageOutcome.Success;
    }

    public async Task<PasskeyManageOutcome> RenameCredentialAsync(Guid userId, Guid credentialId, string nickname)
    {
        User? user = await users.GetByIdAsync(userId);
        if (user is null)
        {
            return PasskeyManageOutcome.Unauthorized;
        }

        UserCredential? cred = await credentials.Query()
            .FirstOrDefaultAsync(c => c.Id == credentialId && c.UserId == user.Id);

        if (cred is null)
        {
            return PasskeyManageOutcome.NotFound;
        }

        cred.Nickname = nickname;
        await credentials.UpdateAsync(cred);
        return PasskeyManageOutcome.Success;
    }
}
