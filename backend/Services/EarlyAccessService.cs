using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Eden_Relics_BE.Data.Entities;
using Eden_Relics_BE.Repositories;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

namespace Eden_Relics_BE.Services;

public interface IEarlyAccessService
{
    /// <summary>A signed link token for one subscriber, valid until <paramref name="expiresUtc"/>.</summary>
    string CreateToken(Guid subscriberId, DateTime expiresUtc);

    /// <summary>
    /// True when the visitor may see early-access pieces: they're signed in with an address on
    /// the list, or they carry a valid, unexpired link token for a subscriber who hasn't left.
    /// </summary>
    Task<bool> HasAccessAsync(ClaimsPrincipal user, string? token);
}

/// <summary>
/// Task 5: subscribers see new pieces before they go public. The newsletter puts a signed token
/// in each subscriber's links (<c>?ea=</c>); the site keeps it and sends it back in the
/// <see cref="HeaderName"/> header. Opening a link never signs anyone up: the token only grants
/// access while the subscriber it was made for is still on the list.
/// </summary>
public class EarlyAccessService(
    IRepository<MailingListSubscriber> subscribers,
    IConfiguration configuration) : IEarlyAccessService
{
    public const string HeaderName = "X-Early-Access";

    /// <summary>How long a link in an early-access email keeps working.</summary>
    public static readonly TimeSpan LinkLifetime = TimeSpan.FromDays(14);

    private byte[] Key => SHA256.HashData(Encoding.UTF8.GetBytes("early-access:" + (configuration["Jwt:Key"] ?? string.Empty)));

    public string CreateToken(Guid subscriberId, DateTime expiresUtc)
    {
        string payload = $"{subscriberId:N}.{new DateTimeOffset(expiresUtc).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)}";
        return $"{payload}.{Sign(payload)}";
    }

    public async Task<bool> HasAccessAsync(ClaimsPrincipal user, string? token)
    {
        if (user.IsInRole("Admin"))
        {
            return true;
        }

        Guid? fromToken = Validate(token, DateTime.UtcNow);
        if (fromToken is Guid id && await subscribers.Query().AnyAsync(s => s.Id == id && !s.Unsubscribed))
        {
            return true;
        }

        string? email = user.FindFirstValue(ClaimTypes.Email)?.Trim().ToLowerInvariant();
        return !string.IsNullOrEmpty(email) && await subscribers.Query().AnyAsync(s => s.Email == email && !s.Unsubscribed);
    }

    /// <summary>The subscriber id in a token, if it's well formed, correctly signed and unexpired.</summary>
    public Guid? Validate(string? token, DateTime utcNow)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 200)
        {
            return null;
        }
        string[] parts = token.Trim().Split('.');
        if (parts.Length != 3
            || !Guid.TryParseExact(parts[0], "N", out Guid id)
            || !long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out long exp)
            || exp > 253402300799)
        {
            return null;
        }
        byte[] expected = Encoding.ASCII.GetBytes(Sign($"{parts[0]}.{parts[1]}"));
        byte[] given = Encoding.ASCII.GetBytes(parts[2]);
        if (!CryptographicOperations.FixedTimeEquals(expected, given))
        {
            return null;
        }
        if (DateTimeOffset.FromUnixTimeSeconds(exp).UtcDateTime <= utcNow)
        {
            return null;
        }
        return id;
    }

    private string Sign(string payload) =>
        WebEncoders.Base64UrlEncode(HMACSHA256.HashData(Key, Encoding.UTF8.GetBytes(payload)));
}
