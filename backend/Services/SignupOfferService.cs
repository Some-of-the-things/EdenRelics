using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using Eden_Relics_BE.Data.Entities;
using Eden_Relics_BE.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Eden_Relics_BE.Services;

/// <summary>The sign-up offer as the admin sets it. Kind is "percent" or "fixed".</summary>
public record SignupOfferSettings(
    bool Enabled,
    string Kind,
    decimal Amount,
    decimal? MinOrder,
    int ValidDays,
    string? SignupText,
    bool ExcludeReduced,
    bool ExcludeGiftOrders,
    bool ShowEarlyAccessLine);

/// <summary>What the sign-up boxes on the site show.</summary>
/// <param name="DiscountOn">True when new subscribers are being sent a code.</param>
/// <param name="Headline">"Join and get 10% off your first order", or null when the offer is off.</param>
/// <param name="Terms">Short small print for the offer (minimum order, exclusions, how long it lasts).</param>
/// <param name="EarlyAccessLine">"Subscribers see new pieces first", when the admin has turned that line on.</param>
public record PublicSignupOfferDto(bool DiscountOn, string? Headline, string? Terms, string? EarlyAccessLine);

public record DiscountCheckRequest(string? Code, string? Email, List<Guid>? ProductIds, bool IsGift);

/// <param name="Description">"10% off your first order", for the basket line.</param>
public record DiscountCheckResult(bool Valid, decimal Discount, string? Message, string? Code, string? Description);

public record AdminDiscountCodeDto(
    Guid Id,
    string Code,
    string Email,
    string Description,
    DateTime IssuedAtUtc,
    DateTime ExpiresAtUtc,
    DateTime? UsedAtUtc,
    Guid? OrderId,
    string Status);

public record AdminDiscountCodesDto(int Issued, int Used, int Unused, int Expired, List<AdminDiscountCodeDto> Codes);

/// <summary>A piece in the basket, as far as a discount is concerned.</summary>
public record DiscountLine(decimal Price, bool Reduced);

/// <summary>The outcome of checking a code against a basket. Error is null when it applies.</summary>
public record DiscountEvaluation(decimal Discount, string? Error);

public class SignupOfferValidationException(string message) : Exception(message);

public interface ISignupOfferService
{
    Task<SignupOfferSettings> GetSettingsAsync();
    Task<SignupOfferSettings> SaveSettingsAsync(SignupOfferSettings settings);
    Task<PublicSignupOfferDto> GetPublicOfferAsync();

    /// <summary>
    /// Sends a new subscriber their code, if the offer is on and this address has never had one.
    /// Never throws: a sign-up must not fail because the email didn't go.
    /// </summary>
    Task IssueSignupCodeAsync(string email, string? unsubscribeToken);

    /// <summary>Checks a code for the basket. Used by the basket preview and again when the order is created.</summary>
    Task<(DiscountCode? Code, DiscountEvaluation Result)> EvaluateAsync(string? code, string? email, IReadOnlyList<DiscountLine> lines, bool isGift);

    Task<DiscountCheckResult> CheckAsync(DiscountCheckRequest request, string? signedInEmail);

    /// <summary>Marks the code used by a paid order. False if another order had already used it.</summary>
    Task<bool> MarkUsedAsync(string code, Guid orderId);

    Task<AdminDiscountCodesDto> GetCodesForAdminAsync(string? status);
}

/// <summary>
/// Task 4: an optional discount for joining the newsletter. When it's on, every new subscriber is
/// emailed a unique, single-use code tied to their address. Each code carries a copy of the terms
/// it was issued under, so turning the offer off or changing it never touches a code already sent.
/// </summary>
public class SignupOfferService(
    IAppSettingsService settings,
    IRepository<DiscountCode> codes,
    IRepository<Data.Entities.Product> products,
    IContentService content,
    INewsletterMailer mailer,
    IConfiguration configuration,
    ILogger<SignupOfferService> logger) : ISignupOfferService
{
    public const string SettingsKey = "signupOffer";
    public const string EarlyAccessLine = "Subscribers see new pieces first";

    /// <summary>The offer the site had before it was configurable: 15% off, no exclusions.</summary>
    public static readonly SignupOfferSettings Defaults = new(
        Enabled: true,
        Kind: "percent",
        Amount: 15,
        MinOrder: null,
        ValidDays: 30,
        SignupText: null,
        ExcludeReduced: false,
        ExcludeGiftOrders: false,
        ShowEarlyAccessLine: false);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    // No 0/O or 1/I/L, so a code read off a phone screen can't be mistyped.
    private const string CodeAlphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    private string FrontendUrl => (configuration["Email:FrontendUrl"] ?? "https://edenrelics.co.uk").TrimEnd('/');
    private string ApiUrl => (configuration["Email:ApiPublicUrl"] ?? "https://api.edenrelics.co.uk").TrimEnd('/');

    public async Task<SignupOfferSettings> GetSettingsAsync()
    {
        string? raw = await settings.GetAsync(SettingsKey);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return Defaults;
        }
        try
        {
            return JsonSerializer.Deserialize<SignupOfferSettings>(raw, Json) ?? Defaults;
        }
        catch (JsonException)
        {
            return Defaults;
        }
    }

    public async Task<SignupOfferSettings> SaveSettingsAsync(SignupOfferSettings s)
    {
        string kind = s.Kind?.Trim().ToLowerInvariant() ?? string.Empty;
        if (kind is not ("percent" or "fixed"))
        {
            throw new SignupOfferValidationException("Choose % off or £ off.");
        }
        if (s.Amount <= 0)
        {
            throw new SignupOfferValidationException("The discount must be more than zero.");
        }
        if (kind == "percent" && s.Amount > 90)
        {
            throw new SignupOfferValidationException("A percentage discount can be at most 90%.");
        }
        if (kind == "fixed" && s.Amount > 500)
        {
            throw new SignupOfferValidationException("A fixed discount can be at most £500.");
        }
        if (s.MinOrder is < 0 or > 10000)
        {
            throw new SignupOfferValidationException("The minimum order must be between £0 and £10,000.");
        }
        if (s.ValidDays is < 1 or > 365)
        {
            throw new SignupOfferValidationException("Codes must last between 1 and 365 days.");
        }
        string? text = string.IsNullOrWhiteSpace(s.SignupText) ? null : s.SignupText.Trim();
        if (text is { Length: > 120 })
        {
            throw new SignupOfferValidationException("Keep the sign-up text to 120 characters.");
        }

        SignupOfferSettings clean = s with
        {
            Kind = kind,
            Amount = Math.Round(s.Amount, 2),
            MinOrder = s.MinOrder is > 0 ? Math.Round(s.MinOrder.Value, 2) : null,
            SignupText = text,
        };
        await settings.SetAsync(SettingsKey, JsonSerializer.Serialize(clean, Json));
        return clean;
    }

    public async Task<PublicSignupOfferDto> GetPublicOfferAsync()
    {
        SignupOfferSettings s = await GetSettingsAsync();
        string? earlyLine = s.ShowEarlyAccessLine ? EarlyAccessLine : null;
        if (!s.Enabled)
        {
            return new PublicSignupOfferDto(false, null, null, earlyLine);
        }
        return new PublicSignupOfferDto(true, s.SignupText ?? DefaultHeadline(s), Terms(s), earlyLine);
    }

    public async Task IssueSignupCodeAsync(string email, string? unsubscribeToken)
    {
        try
        {
            SignupOfferSettings s = await GetSettingsAsync();
            if (!s.Enabled)
            {
                return;
            }
            // One code per address, ever: unsubscribing and joining again doesn't earn another.
            if (await codes.Query().AnyAsync(c => c.Email == email))
            {
                return;
            }

            DiscountCode code = new()
            {
                Email = email,
                Kind = s.Kind == "fixed" ? DiscountKind.Fixed : DiscountKind.Percent,
                Amount = s.Amount,
                MinOrder = s.MinOrder,
                ExcludeReduced = s.ExcludeReduced,
                ExcludeGiftOrders = s.ExcludeGiftOrders,
                ExpiresAtUtc = DateTime.UtcNow.AddDays(s.ValidDays),
            };
            for (int attempt = 0; ; attempt++)
            {
                code.Code = NewCode();
                try
                {
                    await codes.AddAsync(code);
                    break;
                }
                catch (DbUpdateException) when (attempt < 3)
                {
                    // A clash on the unique code: try another.
                }
            }

            try
            {
                await mailer.SendAsync(await BuildWelcomeEmailAsync(code, unsubscribeToken));
            }
            catch
            {
                // Nobody received it: take the code back so signing up again can issue one.
                await codes.RemoveRangeAsync([code]);
                throw;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not issue a sign-up discount code");
        }
    }

    public async Task<(DiscountCode? Code, DiscountEvaluation Result)> EvaluateAsync(
        string? code, string? email, IReadOnlyList<DiscountLine> lines, bool isGift)
    {
        string normalised = NormaliseCode(code);
        if (normalised.Length == 0)
        {
            return (null, new DiscountEvaluation(0, "Enter a code."));
        }
        DiscountCode? found = await codes.Query().FirstOrDefaultAsync(c => c.Code == normalised);
        if (found is null)
        {
            return (null, new DiscountEvaluation(0, "That code isn't recognised."));
        }
        return (found, Evaluate(found, email, lines, isGift, DateTime.UtcNow));
    }

    /// <summary>The rules for one code against one basket. Pure, so it can be tested directly.</summary>
    public static DiscountEvaluation Evaluate(DiscountCode code, string? email, IReadOnlyList<DiscountLine> lines, bool isGift, DateTime utcNow)
    {
        string? address = NewsletterService.NormaliseEmail(email);
        if (address is null || !string.Equals(address, code.Email, StringComparison.OrdinalIgnoreCase))
        {
            return new DiscountEvaluation(0, "This code belongs to a different email address. Use the address it was sent to.");
        }
        if (code.UsedAtUtc is not null)
        {
            return new DiscountEvaluation(0, "This code has already been used.");
        }
        if (code.ExpiresAtUtc <= utcNow)
        {
            return new DiscountEvaluation(0, "This code has expired.");
        }
        if (isGift && code.ExcludeGiftOrders)
        {
            return new DiscountEvaluation(0, "This code can't be used on a gift order.");
        }

        decimal eligible = lines.Where(l => !(code.ExcludeReduced && l.Reduced)).Sum(l => l.Price);
        if (eligible <= 0)
        {
            return new DiscountEvaluation(0, code.ExcludeReduced
                ? "This code doesn't apply to pieces that are already reduced."
                : "Add a piece to your basket first.");
        }
        if (code.MinOrder is decimal min && eligible < min)
        {
            string what = code.ExcludeReduced ? "full-price pieces" : "your order";
            return new DiscountEvaluation(0, $"This code needs {what} to come to at least £{min.ToString("0.##", CultureInfo.InvariantCulture)}.");
        }

        decimal discount = code.Kind == DiscountKind.Percent
            ? Math.Round(eligible * code.Amount / 100m, 2, MidpointRounding.AwayFromZero)
            : Math.Min(code.Amount, eligible);
        return new DiscountEvaluation(discount, null);
    }

    public async Task<DiscountCheckResult> CheckAsync(DiscountCheckRequest request, string? signedInEmail)
    {
        List<Guid> ids = (request.ProductIds ?? []).Distinct().Take(50).ToList();
        List<DiscountLine> lines = await products.Query()
            .Where(p => ids.Contains(p.Id))
            .Select(p => new DiscountLine(p.SalePrice ?? p.Price, p.SalePrice != null))
            .ToListAsync();

        (DiscountCode? code, DiscountEvaluation result) = await EvaluateAsync(request.Code, signedInEmail ?? request.Email, lines, request.IsGift);
        if (code is null || result.Error is not null)
        {
            return new DiscountCheckResult(false, 0, result.Error, null, null);
        }
        return new DiscountCheckResult(true, result.Discount, null, code.Code, Describe(code));
    }

    public async Task<bool> MarkUsedAsync(string code, Guid orderId)
    {
        DiscountCode? found = await codes.Query().FirstOrDefaultAsync(c => c.Code == code);
        if (found is null)
        {
            return false;
        }
        if (found.UsedAtUtc is not null)
        {
            return found.OrderId == orderId;
        }
        found.UsedAtUtc = DateTime.UtcNow;
        found.OrderId = orderId;
        await codes.UpdateAsync(found);
        return true;
    }

    public async Task<AdminDiscountCodesDto> GetCodesForAdminAsync(string? status)
    {
        DateTime now = DateTime.UtcNow;
        List<DiscountCode> all = await codes.Query().OrderByDescending(c => c.CreatedAtUtc).ToListAsync();
        string StatusOf(DiscountCode c) => c.UsedAtUtc is not null ? "used" : c.ExpiresAtUtc <= now ? "expired" : "unused";

        IEnumerable<DiscountCode> shown = status switch
        {
            "used" or "unused" or "expired" => all.Where(c => StatusOf(c) == status),
            _ => all,
        };
        return new AdminDiscountCodesDto(
            all.Count,
            all.Count(c => StatusOf(c) == "used"),
            all.Count(c => StatusOf(c) == "unused"),
            all.Count(c => StatusOf(c) == "expired"),
            shown.Take(500).Select(c => new AdminDiscountCodeDto(
                c.Id, c.Code, c.Email, Describe(c), c.CreatedAtUtc, c.ExpiresAtUtc, c.UsedAtUtc, c.OrderId, StatusOf(c))).ToList());
    }

    public static string NormaliseCode(string? code) =>
        new((code ?? string.Empty).Trim().ToUpperInvariant().Where(ch => ch is (>= 'A' and <= 'Z') or (>= '0' and <= '9') or '-').Take(32).ToArray());

    private static string NewCode()
    {
        Span<char> chars = stackalloc char[8];
        for (int i = 0; i < chars.Length; i++)
        {
            chars[i] = CodeAlphabet[RandomNumberGenerator.GetInt32(CodeAlphabet.Length)];
        }
        return $"EDEN-{chars[..4]}-{chars[4..]}";
    }

    private static string Money(decimal value) =>
        value == Math.Floor(value) ? $"£{value:0}" : $"£{value.ToString("0.00", CultureInfo.InvariantCulture)}";

    private static string AmountText(string kind, decimal amount) =>
        kind == "fixed" ? $"{Money(amount)} off" : $"{amount.ToString("0.##", CultureInfo.InvariantCulture)}% off";

    public static string DefaultHeadline(SignupOfferSettings s)
    {
        string over = s.MinOrder is decimal min ? $" over {Money(min)}" : string.Empty;
        return $"Join and get {AmountText(s.Kind, s.Amount)} your first order{over}";
    }

    private static string Terms(SignupOfferSettings s) =>
        TermsText(s.MinOrder, s.ExcludeReduced, s.ExcludeGiftOrders, $"valid for {s.ValidDays} days");

    private static string TermsText(decimal? minOrder, bool excludeReduced, bool excludeGift, string validity)
    {
        List<string> parts = ["One code per person, sent by email", validity];
        if (minOrder is decimal min)
        {
            parts.Add($"orders of {Money(min)} or more");
        }
        if (excludeReduced)
        {
            parts.Add("not on pieces already reduced");
        }
        if (excludeGift)
        {
            parts.Add("not on gift orders");
        }
        return string.Join(", ", parts) + ".";
    }

    private static string Describe(DiscountCode c) =>
        $"{AmountText(c.Kind == DiscountKind.Fixed ? "fixed" : "percent", c.Amount)} your first order";

    private async Task<OutgoingEmail> BuildWelcomeEmailAsync(DiscountCode code, string? unsubscribeToken)
    {
        string enc(string v) => WebUtility.HtmlEncode(v);
        string until = code.ExpiresAtUtc.ToString("d MMMM yyyy", CultureInfo.GetCultureInfo("en-GB"));
        string terms = TermsText(code.MinOrder, code.ExcludeReduced, code.ExcludeGiftOrders, $"use it by {until}");
        string body = $"""
            <h1>Welcome to Eden Relics</h1>
            <p>Thank you for joining. Every piece here is one of a kind, found, checked and cared for by hand, and you'll be among the first to hear when new ones arrive.</p>
            <p>Here's {enc(Describe(code))}:</p>
            <p style="text-align:center;margin:24px 0;"><span style="display:inline-block;padding:14px 26px;border:2px dashed #9B4A1E;font-family:'Courier New',monospace;font-size:22px;letter-spacing:3px;color:#2E1A0E;background:#FFFFFF;">{enc(code.Code)}</span></p>
            <p>Enter it at checkout with this email address. {enc(terms)}</p>
            <p style="text-align:center;margin:28px 0 8px;"><a href="{enc(FrontendUrl)}/shop" style="display:inline-block;padding:12px 26px;background:#2E1A0E;color:#F5F0E6;text-decoration:none;font-family:Georgia,'Times New Roman',serif;">Browse the shop</a></p>
            <p>Thank you,<br>Eden Relics</p>
            """;

        Dictionary<string, string> all = await content.GetAllAsync(null);
        string t = string.IsNullOrEmpty(unsubscribeToken) ? "missing" : unsubscribeToken;
        RenderedEmail rendered = EmailTemplate.Render(
            "Your welcome code",
            $"{Describe(code)}: {code.Code}",
            body,
            EmailTemplate.FooterFrom(all),
            FrontendUrl,
            $"{FrontendUrl}/unsubscribe?t={t}");

        Dictionary<string, string> headers = new()
        {
            ["List-Unsubscribe"] = $"<{ApiUrl}/api/mailing-list/one-click/{t}>",
            ["List-Unsubscribe-Post"] = "List-Unsubscribe=One-Click",
        };
        return new OutgoingEmail(code.Email, "Your Eden Relics welcome code", rendered.Html, rendered.Text, headers);
    }
}
