using System.Net.Mail;
using System.Security.Cryptography;
using Eden_Relics_BE.Data.Entities;
using Eden_Relics_BE.DTOs;
using Eden_Relics_BE.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Eden_Relics_BE.Services;

public class NewsletterValidationException(string message) : Exception(message);

public interface INewsletterService
{
    Task<List<NewsletterDto>> ListAsync();
    Task<NewsletterDto?> GetAsync(Guid id);
    Task<NewsletterDto> CreateAsync(SaveNewsletterRequest request);
    Task<NewsletterDto?> UpdateAsync(Guid id, SaveNewsletterRequest request);
    Task<bool> DeleteAsync(Guid id);
    Task<NewsletterPreviewDto> PreviewAsync(NewsletterPreviewRequest request);
    Task<bool> SendTestAsync(Guid id, string email);
    Task<NewsletterDto?> SendAsync(Guid id, DateTime? scheduledForUtc);
    Task<NewsletterDto?> CancelScheduleAsync(Guid id);
    Task<int> GetAudienceCountAsync();

    /// <summary>Starts any scheduled sends that are due and works through queued batches. Returns the number of emails handed to the provider.</summary>
    Task<int> ProcessQueueAsync(CancellationToken ct = default);
}

/// <summary>
/// Newsletters written and sent from the admin.
///
/// Sending snapshots every active subscriber into <see cref="NewsletterDelivery"/> rows, then
/// sends them in batches of <see cref="BatchSize"/>. Each batch is claimed (numbered and saved)
/// before it goes to the provider, and its number is part of the provider's idempotency key, so
/// a crash or lost response is retried with the same key and never double-sends; nobody is
/// skipped because unclaimed rows stay pending. Unsubscribing takes effect for every batch not yet
/// claimed.
/// </summary>
public class NewsletterService(
    IRepository<Newsletter> newsletters,
    IRepository<NewsletterDelivery> deliveries,
    IRepository<MailingListSubscriber> subscribers,
    IContentService content,
    INewsletterMailer mailer,
    IEarlyAccessService earlyAccess,
    IConfiguration configuration,
    ILogger<NewsletterService> logger) : INewsletterService
{
    public const int BatchSize = 50;
    public const int MaxAttempts = 3;
    public const int MaxSubjectLength = 200;
    public const int MaxPreviewTextLength = 200;
    public const int MaxBodyLength = 500_000;

    private const string TokenPlaceholder = "__ER_UNSUBSCRIBE_TOKEN__";

    /// <summary>Put in early-access links by the admin's "early access drop"; replaced with each subscriber's own signed token.</summary>
    public const string EarlyAccessPlaceholder = "__ER_EARLY_ACCESS__";

    private string FrontendUrl => (configuration["Email:FrontendUrl"] ?? "https://edenrelics.co.uk").TrimEnd('/');
    private string ApiUrl => (configuration["Email:ApiPublicUrl"] ?? "https://api.edenrelics.co.uk").TrimEnd('/');

    public async Task<List<NewsletterDto>> ListAsync()
    {
        List<Newsletter> rows = await newsletters.Query()
            .OrderByDescending(n => n.SentAtUtc ?? n.ScheduledForUtc ?? n.UpdatedAtUtc)
            .ToListAsync();
        List<Guid> ids = rows.Select(n => n.Id).ToList();
        var stats = await deliveries.Query()
            .Where(d => ids.Contains(d.NewsletterId))
            .GroupBy(d => d.NewsletterId)
            .Select(g => new
            {
                Id = g.Key,
                Opened = g.Count(d => d.OpenedAtUtc != null),
                Clicked = g.Count(d => d.ClickedAtUtc != null),
                Bounced = g.Count(d => d.BouncedAtUtc != null),
            })
            .ToDictionaryAsync(x => x.Id);
        // The list doesn't need the bodies.
        return rows.Select(n =>
        {
            NewsletterDto dto = ToDto(n, includeBody: false);
            return stats.TryGetValue(n.Id, out var s)
                ? dto with { OpenedCount = s.Opened, ClickedCount = s.Clicked, BouncedCount = s.Bounced }
                : dto;
        }).ToList();
    }

    public async Task<NewsletterDto?> GetAsync(Guid id)
    {
        Newsletter? n = await newsletters.GetByIdAsync(id);
        return n is null ? null : ToDto(n, includeBody: true);
    }

    public async Task<NewsletterDto> CreateAsync(SaveNewsletterRequest request)
    {
        Newsletter n = new();
        Apply(n, request);
        await newsletters.AddAsync(n);
        return ToDto(n, includeBody: true);
    }

    public async Task<NewsletterDto?> UpdateAsync(Guid id, SaveNewsletterRequest request)
    {
        Newsletter? n = await newsletters.GetByIdAsync(id);
        if (n is null)
        {
            return null;
        }
        if (n.Status is NewsletterStatus.Sending or NewsletterStatus.Sent)
        {
            throw new NewsletterValidationException("This newsletter has already been sent and can't be changed.");
        }
        Apply(n, request);
        await newsletters.UpdateAsync(n);
        return ToDto(n, includeBody: true);
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        Newsletter? n = await newsletters.GetByIdAsync(id);
        if (n is null)
        {
            return false;
        }
        if (n.Status != NewsletterStatus.Draft)
        {
            throw new NewsletterValidationException("Only drafts can be deleted. Cancel the schedule first.");
        }
        await newsletters.DeleteAsync(id);
        return true;
    }

    public async Task<NewsletterPreviewDto> PreviewAsync(NewsletterPreviewRequest request)
    {
        string body = EmailTemplate.SanitizeBody(Limit(request.BodyHtml, MaxBodyLength));
        EmailFooterInfo footer = await FooterAsync();
        RenderedEmail rendered = EmailTemplate.Render(
            request.Subject ?? string.Empty,
            request.PreviewText,
            body,
            footer,
            FrontendUrl,
            $"{FrontendUrl}/unsubscribe");
        return new NewsletterPreviewDto(
            rendered.Html.Replace(EarlyAccessPlaceholder, "preview"),
            rendered.Text.Replace(EarlyAccessPlaceholder, "preview"),
            body);
    }

    public async Task<bool> SendTestAsync(Guid id, string email)
    {
        Newsletter? n = await newsletters.GetByIdAsync(id);
        if (n is null)
        {
            return false;
        }
        string address = NormaliseEmail(email) ?? throw new NewsletterValidationException("Enter a valid email address for the test.");
        EnsureSendable(n);

        EmailFooterInfo footer = await FooterAsync();
        RenderedEmail rendered = EmailTemplate.Render(n.Subject, n.PreviewText, n.BodyHtml, footer, FrontendUrl, $"{FrontendUrl}/unsubscribe");
        // Early-access links in a test work only if the test address is itself on the list.
        Guid? subscriberId = await subscribers.Query().Where(s => s.Email == address && !s.Unsubscribed).Select(s => (Guid?)s.Id).FirstOrDefaultAsync();
        string ea = subscriberId is Guid sid ? earlyAccess.CreateToken(sid, DateTime.UtcNow.Add(EarlyAccessService.LinkLifetime)) : "preview";
        await mailer.SendAsync(new OutgoingEmail(
            address,
            $"[Test] {n.Subject}",
            rendered.Html.Replace(EarlyAccessPlaceholder, ea),
            rendered.Text.Replace(EarlyAccessPlaceholder, ea)));
        return true;
    }

    public async Task<NewsletterDto?> SendAsync(Guid id, DateTime? scheduledForUtc)
    {
        Newsletter? n = await newsletters.GetByIdAsync(id);
        if (n is null)
        {
            return null;
        }
        if (n.Status is NewsletterStatus.Sending or NewsletterStatus.Sent)
        {
            throw new NewsletterValidationException("This newsletter has already been sent.");
        }
        EnsureSendable(n);

        DateTime now = DateTime.UtcNow;
        if (scheduledForUtc is DateTime when && when > now.AddMinutes(1))
        {
            if (when > now.AddYears(1))
            {
                throw new NewsletterValidationException("Pick a date within the next year.");
            }
            if (await subscribers.CountAsync(s => !s.Unsubscribed) == 0)
            {
                throw new NewsletterValidationException("There are no active subscribers to send to.");
            }
            n.Status = NewsletterStatus.Scheduled;
            n.ScheduledForUtc = DateTime.SpecifyKind(when, DateTimeKind.Utc);
            n.LastError = null;
            await newsletters.UpdateAsync(n);
            return ToDto(n, includeBody: true);
        }

        await StartSendingAsync(n);
        NewsletterBackgroundService.Wake();
        return ToDto(n, includeBody: true);
    }

    public async Task<NewsletterDto?> CancelScheduleAsync(Guid id)
    {
        Newsletter? n = await newsletters.GetByIdAsync(id);
        if (n is null)
        {
            return null;
        }
        if (n.Status != NewsletterStatus.Scheduled)
        {
            throw new NewsletterValidationException("Only a scheduled newsletter can be cancelled.");
        }
        n.Status = NewsletterStatus.Draft;
        n.ScheduledForUtc = null;
        await newsletters.UpdateAsync(n);
        return ToDto(n, includeBody: true);
    }

    public async Task<int> GetAudienceCountAsync()
    {
        return await subscribers.CountAsync(s => !s.Unsubscribed);
    }

    public async Task<int> ProcessQueueAsync(CancellationToken ct = default)
    {
        DateTime now = DateTime.UtcNow;

        List<Newsletter> due = await newsletters.Query()
            .Where(n => n.Status == NewsletterStatus.Scheduled && n.ScheduledForUtc <= now)
            .ToListAsync(ct);
        foreach (Newsletter n in due)
        {
            try
            {
                EnsureSendable(n);
                await StartSendingAsync(n);
            }
            catch (NewsletterValidationException ex)
            {
                n.Status = NewsletterStatus.Draft;
                n.LastError = $"Scheduled send didn't start: {ex.Message}";
                await newsletters.UpdateAsync(n);
            }
        }

        List<Newsletter> sending = await newsletters.Query()
            .Where(n => n.Status == NewsletterStatus.Sending)
            .OrderBy(n => n.SendStartedAtUtc)
            .ToListAsync(ct);

        int sent = 0;
        foreach (Newsletter n in sending)
        {
            sent += await WorkThroughAsync(n, ct);
        }
        return sent;
    }

    private async Task StartSendingAsync(Newsletter n)
    {
        List<MailingListSubscriber> active = await subscribers.Query()
            .Where(s => !s.Unsubscribed)
            .OrderBy(s => s.CreatedAtUtc)
            .ToListAsync();
        if (active.Count == 0)
        {
            throw new NewsletterValidationException("There are no active subscribers to send to.");
        }

        foreach (MailingListSubscriber s in active.Where(s => string.IsNullOrEmpty(s.UnsubscribeToken)))
        {
            s.UnsubscribeToken = NewToken();
        }

        n.Status = NewsletterStatus.Sending;
        n.SendStartedAtUtc = DateTime.UtcNow;
        n.RecipientCount = active.Count;
        n.LastError = null;

        // One save: the tokens, the newsletter's new state and the recipient snapshot together.
        await deliveries.AddRangeAsync(active.Select(s => new NewsletterDelivery
        {
            NewsletterId = n.Id,
            SubscriberId = s.Id,
            Email = s.Email,
        }));
    }

    private async Task<int> WorkThroughAsync(Newsletter n, CancellationToken ct)
    {
        RenderedEmail? template = null;
        int sent = 0;

        while (!ct.IsCancellationRequested)
        {
            // A claimed batch that never finished comes first, retried under its original number.
            List<NewsletterDelivery> batch = await deliveries.Query()
                .Where(d => d.NewsletterId == n.Id && d.Status == NewsletterDeliveryStatus.Sending)
                .OrderBy(d => d.BatchNumber).ThenBy(d => d.Id)
                .ToListAsync(ct);
            if (batch.Count > 0)
            {
                int first = batch[0].BatchNumber ?? 0;
                batch = batch.Where(d => d.BatchNumber == first).ToList();
            }
            else
            {
                batch = await deliveries.Query()
                    .Where(d => d.NewsletterId == n.Id && d.Status == NewsletterDeliveryStatus.Pending)
                    .OrderBy(d => d.Id)
                    .Take(BatchSize)
                    .ToListAsync(ct);
                if (batch.Count == 0)
                {
                    await FinishAsync(n);
                    return sent;
                }

                // Unsubscribed since the send started: skip, before the batch is fixed.
                List<Guid> ids = batch.Select(d => d.SubscriberId).ToList();
                HashSet<Guid> stillActive = (await subscribers.Query()
                    .Where(s => ids.Contains(s.Id) && !s.Unsubscribed)
                    .Select(s => s.Id)
                    .ToListAsync(ct)).ToHashSet();
                foreach (NewsletterDelivery d in batch.Where(d => !stillActive.Contains(d.SubscriberId)))
                {
                    d.Status = NewsletterDeliveryStatus.Skipped;
                }
                batch = batch.Where(d => d.Status == NewsletterDeliveryStatus.Pending).ToList();

                int number = n.NextBatchNumber++;
                foreach (NewsletterDelivery d in batch)
                {
                    d.Status = NewsletterDeliveryStatus.Sending;
                    d.BatchNumber = number;
                }
                await deliveries.AddRangeAsync([]); // claim the batch (and the skips) before sending
                if (batch.Count == 0)
                {
                    continue;
                }
            }

            template ??= await RenderTemplateAsync(n);
            List<Guid> batchIds = batch.Select(d => d.SubscriberId).ToList();
            Dictionary<Guid, string> tokens = await subscribers.Query()
                .Where(s => batchIds.Contains(s.Id))
                .ToDictionaryAsync(s => s.Id, s => s.UnsubscribeToken ?? string.Empty, ct);

            List<OutgoingEmail> emails = batch.Select(d => Personalise(n, template, d, tokens.GetValueOrDefault(d.SubscriberId))).ToList();
            string key = $"newsletter-{n.Id:N}-batch-{batch[0].BatchNumber}";

            foreach (NewsletterDelivery d in batch)
            {
                d.Attempts++;
            }

            try
            {
                BatchOutcome outcome = await mailer.SendBatchAsync(key, emails, ct);
                Dictionary<int, string> failed = outcome.Failures.ToDictionary(f => f.Index, f => f.Message);
                DateTime at = DateTime.UtcNow;
                for (int i = 0; i < batch.Count; i++)
                {
                    if (failed.TryGetValue(i, out string? message))
                    {
                        batch[i].Status = NewsletterDeliveryStatus.Failed;
                        batch[i].Error = Limit(message, 500);
                    }
                    else
                    {
                        batch[i].Status = NewsletterDeliveryStatus.Sent;
                        batch[i].SentAtUtc = at;
                        batch[i].ProviderId = i < outcome.ProviderIds.Count ? outcome.ProviderIds[i] : null;
                        sent++;
                    }
                }
                await UpdateCountsAsync(n);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Newsletter {NewsletterId} batch {Batch} failed (attempt {Attempt})", n.Id, batch[0].BatchNumber, batch[0].Attempts);
                foreach (NewsletterDelivery d in batch.Where(d => d.Attempts >= MaxAttempts))
                {
                    d.Status = NewsletterDeliveryStatus.Failed;
                    d.Error = Limit(ex.Message, 500);
                }
                n.LastError = Limit(ex.Message, 1000);
                await UpdateCountsAsync(n);
                // Leave the rest for the next pass rather than hammering a failing provider.
                return sent;
            }
        }
        return sent;
    }

    private async Task UpdateCountsAsync(Newsletter n)
    {
        await deliveries.AddRangeAsync([]); // persist the batch outcome first
        var counts = await deliveries.Query()
            .Where(d => d.NewsletterId == n.Id)
            .GroupBy(d => d.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();
        n.SentCount = counts.FirstOrDefault(c => c.Status == NewsletterDeliveryStatus.Sent)?.Count ?? 0;
        n.FailedCount = counts.FirstOrDefault(c => c.Status == NewsletterDeliveryStatus.Failed)?.Count ?? 0;
        n.SkippedCount = counts.FirstOrDefault(c => c.Status == NewsletterDeliveryStatus.Skipped)?.Count ?? 0;
        await newsletters.UpdateAsync(n);
    }

    private async Task FinishAsync(Newsletter n)
    {
        n.Status = NewsletterStatus.Sent;
        n.SentAtUtc = DateTime.UtcNow;
        await UpdateCountsAsync(n);
    }

    private async Task<RenderedEmail> RenderTemplateAsync(Newsletter n)
    {
        EmailFooterInfo footer = await FooterAsync();
        return EmailTemplate.Render(n.Subject, n.PreviewText, n.BodyHtml, footer, FrontendUrl, $"{FrontendUrl}/unsubscribe?t={TokenPlaceholder}");
    }

    private OutgoingEmail Personalise(Newsletter n, RenderedEmail template, NewsletterDelivery d, string? token)
    {
        string t = string.IsNullOrEmpty(token) ? "missing" : token;
        Dictionary<string, string> headers = new()
        {
            // RFC 8058 one-click unsubscribe: Gmail and Yahoo require both headers for bulk mail.
            ["List-Unsubscribe"] = $"<{ApiUrl}/api/mailing-list/one-click/{t}>",
            ["List-Unsubscribe-Post"] = "List-Unsubscribe=One-Click",
        };
        string html = template.Html.Replace(TokenPlaceholder, t);
        string text = template.Text.Replace(TokenPlaceholder, t);
        if (html.Contains(EarlyAccessPlaceholder, StringComparison.Ordinal))
        {
            string ea = earlyAccess.CreateToken(d.SubscriberId, DateTime.UtcNow.Add(EarlyAccessService.LinkLifetime));
            html = html.Replace(EarlyAccessPlaceholder, ea);
            text = text.Replace(EarlyAccessPlaceholder, ea);
        }
        return new OutgoingEmail(d.Email, n.Subject, html, text, headers);
    }

    private async Task<EmailFooterInfo> FooterAsync()
    {
        Dictionary<string, string> all = await content.GetAllAsync(null);
        return EmailTemplate.FooterFrom(all);
    }

    private static void Apply(Newsletter n, SaveNewsletterRequest request)
    {
        string subject = (request.Subject ?? string.Empty).Trim();
        string preview = (request.PreviewText ?? string.Empty).Trim();
        string body = request.BodyHtml ?? string.Empty;
        if (subject.Length > MaxSubjectLength)
        {
            throw new NewsletterValidationException($"The subject can be at most {MaxSubjectLength} characters.");
        }
        if (preview.Length > MaxPreviewTextLength)
        {
            throw new NewsletterValidationException($"The preview text can be at most {MaxPreviewTextLength} characters.");
        }
        if (body.Length > MaxBodyLength)
        {
            throw new NewsletterValidationException("The email is too long.");
        }
        n.Subject = subject;
        n.PreviewText = preview;
        n.BodyHtml = EmailTemplate.SanitizeBody(body);
        n.EditorMode = request.EditorMode == "html" ? "html" : "visual";
    }

    private static void EnsureSendable(Newsletter n)
    {
        if (string.IsNullOrWhiteSpace(n.Subject))
        {
            throw new NewsletterValidationException("Add a subject line first.");
        }
        if (string.IsNullOrWhiteSpace(EmailTemplate.ToPlainText(n.BodyHtml)) && !n.BodyHtml.Contains("<img", StringComparison.OrdinalIgnoreCase))
        {
            throw new NewsletterValidationException("The email is empty.");
        }
    }

    public static string NewToken()
    {
        return Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
    }

    public static string? NormaliseEmail(string? email)
    {
        string trimmed = (email ?? string.Empty).Trim().ToLowerInvariant();
        if (trimmed.Length is < 6 or > 254)
        {
            return null;
        }
        if (!MailAddress.TryCreate(trimmed, out MailAddress? parsed) || parsed.Address != trimmed)
        {
            return null;
        }
        string host = parsed.Host;
        if (!host.Contains('.') || host.StartsWith('.') || host.EndsWith('.') || host.Contains(".."))
        {
            return null;
        }
        return trimmed;
    }

    private static string Limit(string? value, int max)
    {
        string v = value ?? string.Empty;
        return v.Length <= max ? v : v[..max];
    }

    private static NewsletterDto ToDto(Newsletter n, bool includeBody) => new(
        n.Id,
        n.Subject,
        n.PreviewText,
        includeBody ? n.BodyHtml : string.Empty,
        n.EditorMode,
        n.Status.ToString(),
        n.CreatedAtUtc,
        n.UpdatedAtUtc,
        n.ScheduledForUtc,
        n.SendStartedAtUtc,
        n.SentAtUtc,
        n.RecipientCount,
        n.SentCount,
        n.FailedCount,
        n.SkippedCount,
        n.LastError);
}
