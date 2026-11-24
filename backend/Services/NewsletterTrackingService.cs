using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Eden_Relics_BE.Data.Entities;
using Eden_Relics_BE.Repositories;
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace Eden_Relics_BE.Services;

public interface IEmailImageService
{
    /// <summary>
    /// A JPEG copy of one of the shop's own images, sized for email. Product photos are WebP,
    /// which Outlook on Windows won't show. Returns null if the URL isn't one of ours.
    /// </summary>
    Task<string?> ToEmailJpegAsync(string url, CancellationToken ct = default);
}

public class EmailImageService(ImageStorageService storage, IHttpClientFactory httpFactory, ILogger<EmailImageService> logger) : IEmailImageService
{
    public const int Width = 600;
    private const long MaxDownloadBytes = 25 * 1024 * 1024;

    public async Task<string?> ToEmailJpegAsync(string url, CancellationToken ct = default)
    {
        // Only ever fetch from our own image bucket.
        if (!storage.IsConfigured
            || string.IsNullOrWhiteSpace(url)
            || !Uri.TryCreate(url, UriKind.Absolute, out Uri? uri)
            || uri.Scheme != Uri.UriSchemeHttps
            || !url.StartsWith(storage.PublicUrl.TrimEnd('/') + "/", StringComparison.Ordinal))
        {
            return null;
        }
        if (uri.AbsolutePath.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
            || uri.AbsolutePath.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)
            || uri.AbsolutePath.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
        {
            return url;
        }

        string key = "email/" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(url)))[..32] + ".jpg";

        HttpClient http = httpFactory.CreateClient();
        http.Timeout = TimeSpan.FromSeconds(30);
        using HttpResponseMessage response = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode || response.Content.Headers.ContentLength > MaxDownloadBytes)
        {
            logger.LogWarning("Could not fetch {Url} for an email image ({Status})", url, (int)response.StatusCode);
            return null;
        }

        await ImageUploadHelper.ProcessingGate.WaitAsync(ct);
        try
        {
            await using Stream source = await response.Content.ReadAsStreamAsync(ct);
            using Image image = await Image.LoadAsync(source, ct);
            if (image.Width > Width)
            {
                image.Mutate(x => x.Resize(new ResizeOptions { Size = new Size(Width, 0), Mode = ResizeMode.Max }));
            }
            using MemoryStream output = new();
            await image.SaveAsync(output, new JpegEncoder { Quality = 82 }, ct);
            output.Position = 0;
            return await storage.UploadAsync(output, key, "image/jpeg");
        }
        finally
        {
            ImageUploadHelper.ProcessingGate.Release();
        }
    }
}

public interface INewsletterTrackingService
{
    /// <summary>Checks the provider's signature on a webhook call.</summary>
    bool Verify(string? id, string? timestamp, string? signature, string body, DateTime utcNow);

    /// <summary>Records an open, click, bounce or complaint. Bounces and complaints take the address off the list.</summary>
    Task HandleAsync(string body);
}

/// <summary>
/// Open, click and bounce figures for newsletters, from Resend's webhooks. Nothing happens unless
/// <c>Resend:WebhookSecret</c> is set and the webhook (plus open/click tracking) is switched on in
/// Resend. Webhooks are signed the Svix way: HMAC-SHA256 over "id.timestamp.body".
/// </summary>
public class NewsletterTrackingService(
    IRepository<NewsletterDelivery> deliveries,
    IRepository<MailingListSubscriber> subscribers,
    IConfiguration configuration,
    ILogger<NewsletterTrackingService> logger) : INewsletterTrackingService
{
    public static readonly TimeSpan Tolerance = TimeSpan.FromMinutes(5);

    public bool Verify(string? id, string? timestamp, string? signature, string body, DateTime utcNow)
    {
        string? secret = configuration["Resend:WebhookSecret"];
        if (string.IsNullOrWhiteSpace(secret) || string.IsNullOrEmpty(id) || string.IsNullOrEmpty(timestamp) || string.IsNullOrEmpty(signature))
        {
            return false;
        }
        if (!long.TryParse(timestamp, out long seconds) || seconds > 253402300799 || seconds < 0
            || (utcNow - DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime).Duration() > Tolerance)
        {
            return false;
        }

        byte[] key;
        try
        {
            key = Convert.FromBase64String(secret.StartsWith("whsec_", StringComparison.Ordinal) ? secret[6..] : secret);
        }
        catch (FormatException)
        {
            logger.LogError("Resend:WebhookSecret is not a valid signing secret");
            return false;
        }
        byte[] expected = Encoding.ASCII.GetBytes(Convert.ToBase64String(HMACSHA256.HashData(key, Encoding.UTF8.GetBytes($"{id}.{timestamp}.{body}"))));

        // The header can carry several "v1,<sig>" entries (during secret rotation).
        foreach (string part in signature.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            int comma = part.IndexOf(',');
            if (comma > 0 && part[..comma] == "v1"
                && CryptographicOperations.FixedTimeEquals(expected, Encoding.ASCII.GetBytes(part[(comma + 1)..])))
            {
                return true;
            }
        }
        return false;
    }

    public async Task HandleAsync(string body)
    {
        string? type;
        string? emailId;
        try
        {
            using JsonDocument doc = JsonDocument.Parse(body);
            type = doc.RootElement.TryGetProperty("type", out JsonElement t) ? t.GetString() : null;
            emailId = doc.RootElement.TryGetProperty("data", out JsonElement data) && data.TryGetProperty("email_id", out JsonElement e)
                ? e.GetString()
                : null;
        }
        catch (JsonException)
        {
            return;
        }
        if (string.IsNullOrEmpty(type) || string.IsNullOrEmpty(emailId))
        {
            return;
        }

        NewsletterDelivery? delivery = await deliveries.Query().FirstOrDefaultAsync(d => d.ProviderId == emailId);
        if (delivery is null)
        {
            return; // not a newsletter (order emails, review requests, tests)
        }

        DateTime now = DateTime.UtcNow;
        switch (type)
        {
            case "email.opened":
                delivery.OpenedAtUtc ??= now;
                break;
            case "email.clicked":
                delivery.ClickedAtUtc ??= now;
                // A click means it was opened, even if the open pixel was blocked.
                delivery.OpenedAtUtc ??= now;
                break;
            case "email.bounced":
            case "email.complained":
                if (type == "email.bounced")
                {
                    delivery.BouncedAtUtc ??= now;
                }
                // Keep sending to an address that bounced or reported spam and the domain's
                // reputation suffers: take it off the list.
                MailingListSubscriber? sub = await subscribers.Query().FirstOrDefaultAsync(s => s.Id == delivery.SubscriberId);
                if (sub is not null && !sub.Unsubscribed)
                {
                    sub.Unsubscribed = true;
                    sub.UnsubscribedAtUtc = now;
                }
                break;
            default:
                return;
        }
        await deliveries.AddRangeAsync([]); // flush the tracked changes
    }
}
