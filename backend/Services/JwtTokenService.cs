using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Eden_Relics_BE.Data.Entities;
using Microsoft.IdentityModel.Tokens;

namespace Eden_Relics_BE.Services;

/// <summary>
/// Centralises JWT creation so every issuer (password, passkey, external, refresh,
/// password-change) embeds the same claims — including the token_version used to
/// revoke tokens when credentials change.
/// </summary>
public class JwtTokenService(IConfiguration configuration)
{
    public string GenerateToken(User user)
    {
        SigningCredentials credentials = SigningCredentials();

        Claim[] claims =
        [
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role),
            new(ClaimTypes.GivenName, user.FirstName),
            new("token_version", user.TokenVersion.ToString()),
        ];

        JwtSecurityToken token = new(
            issuer: configuration["Jwt:Issuer"],
            audience: configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddDays(7),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateMfaToken(User user)
    {
        SigningCredentials credentials = SigningCredentials();

        Claim[] claims =
        [
            new("mfa_user_id", user.Id.ToString()),
            new("purpose", "mfa"),
        ];

        JwtSecurityToken token = new(
            issuer: configuration["Jwt:Issuer"],
            audience: configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private SigningCredentials SigningCredentials()
    {
        string key = configuration["Jwt:Key"]!;
        SymmetricSecurityKey securityKey = new(Encoding.UTF8.GetBytes(key));
        return new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);
    }
}
