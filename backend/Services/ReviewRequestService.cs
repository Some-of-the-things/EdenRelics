using System.Net;
using Eden_Relics_BE.Data.Entities;
using Eden_Relics_BE.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Eden_Relics_BE.Services;

public interface IReviewRequestService
{
    /// <summary>Emails a review link for every order dispatched at least the set number of days ago that hasn't had one. Returns how many were sent.</summary>
    Task<int> SendDueAsync(CancellationToken ct = default);

    Task<int> GetDelayDaysAsync();
    Task SetDelayDaysAsync(int days);

    /// <summary>Days after the first email to send the one reminder; 0 means no reminder.</summary>
    Task<int> GetReminderDaysAsync();
    Task SetReminderDaysAsync(int days);

    /// <summary>The email a buyer gets, for an order (used by the sender and the tests).</summary>
    Task<OutgoingEmail> BuildEmailAsync(Order order, string token, bool reminder = false);
}

/// <summary>
/// The "how was your order?" email, sent a set number of days (default 7, changeable in the admin)
/// after an order is first marked Shipped. The link carries a token tied to that order, so only
/// the buyer can use it and they don't need to sign in. Orders dispatched before this feature
/// existed have no dispatch time, so they are never emailed.
/// </summary>
public class ReviewRequestService(
    IRepository<Order> orders,
    IRepository<Review> reviews,
    IAppSettingsService settings,
    IContentService content,
    INewsletterMailer mailer,
    IConfiguration configuration,
    ILogger<ReviewRequestService> logger) : IReviewRequestService
{
    public const int DefaultDelayDays = 7;
    public const int MinDelayDays = 1;
    public const int MaxDelayDays = 60;
    public const int DefaultReminderDays = 7;
    public const int MaxReminderDays = 30;

    private string FrontendUrl => (configuration["Email:FrontendUrl"] ?? "https://edenrelics.co.uk").TrimEnd('/');

    public async Task<int> GetDelayDaysAsync()
    {
        int days = await settings.GetIntAsync(AppSettingsService.ReviewRequestDelayDays, DefaultDelayDays);
        return Math.Clamp(days, MinDelayDays, MaxDelayDays);
    }

    public async Task SetDelayDaysAsync(int days)
    {
        if (days is < MinDelayDays or > MaxDelayDays)
        {
            throw new ArgumentOutOfRangeException(nameof(days), $"Choose between {MinDelayDays} and {MaxDelayDays} days.");
        }
        await settings.SetAsync(AppSettingsService.ReviewRequestDelayDays, days.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    public async Task<int> GetReminderDaysAsync()
    {
        int days = await settings.GetIntAsync(AppSettingsService.ReviewReminderDays, DefaultReminderDays);
        return Math.Clamp(days, 0, MaxReminderDays);
    }

    public async Task SetReminderDaysAsync(int days)
    {
        if (days is < 0 or > MaxReminderDays)
        {
            throw new ArgumentOutOfRangeException(nameof(days), $"Choose between 0 and {MaxReminderDays} days.");
        }
        await settings.SetAsync(AppSettingsService.ReviewReminderDays, days.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    public async Task<int> SendDueAsync(CancellationToken ct = default)
    {
        int sent = await SendFirstRequestsAsync(ct);
        return sent + await SendRemindersAsync(ct);
    }

    /// <summary>
    /// One gentle reminder, a set number of days after the first email, only if there's still no
    /// review. Never more than one.
    /// </summary>
    private async Task<int> SendRemindersAsync(CancellationToken ct)
    {
        int days = await GetReminderDaysAsync();
        if (days == 0)
        {
            return 0;
        }
        DateTime cutoff = DateTime.UtcNow.AddDays(-days);

        List<Order> due = await orders.Query()
            .Include(o => o.User)
            .Include(o => o.Items)
            .Where(o => o.ReviewRequestSentAtUtc != null
                && o.ReviewRequestSentAtUtc <= cutoff
                && o.ReviewReminderSentAtUtc == null
                && o.ReviewToken != null
                && ReviewService.DispatchedStatuses.Contains(o.Status))
            .Where(o => !reviews.Query(true).Any(r => r.OrderId == o.Id))
            .OrderBy(o => o.ReviewRequestSentAtUtc)
            .Take(50)
            .ToListAsync(ct);

        int sent = 0;
        foreach (Order order in due)
        {
            string? to = !string.IsNullOrWhiteSpace(order.User?.Email) ? order.User!.Email : order.GuestEmail;
            order.ReviewReminderSentAtUtc = DateTime.UtcNow;
            await orders.UpdateAsync(order);
            if (string.IsNullOrWhiteSpace(to))
            {
                continue;
            }
            try
            {
                await mailer.SendAsync(await BuildEmailAsync(order, order.ReviewToken!, reminder: true), ct);
                sent++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Review reminder for order {OrderId} failed; will retry", order.Id);
                order.ReviewReminderSentAtUtc = null;
                await orders.UpdateAsync(order);
            }
        }
        return sent;
    }

    private async Task<int> SendFirstRequestsAsync(CancellationToken ct)
    {
        int days = await GetDelayDaysAsync();
        DateTime cutoff = DateTime.UtcNow.AddDays(-days);

        List<Order> due = await orders.Query()
            .Include(o => o.User)
            .Include(o => o.Items)
            .Where(o => o.DispatchedAtUtc != null
                && o.DispatchedAtUtc <= cutoff
                && o.ReviewRequestSentAtUtc == null
                && ReviewService.DispatchedStatuses.Contains(o.Status))
            .Where(o => !reviews.Query(true).Any(r => r.OrderId == o.Id))
            .OrderBy(o => o.DispatchedAtUtc)
            .Take(50)
            .ToListAsync(ct);

        int sent = 0;
        foreach (Order order in due)
        {
            string? to = !string.IsNullOrWhiteSpace(order.User?.Email) ? order.User!.Email : order.GuestEmail;
            order.ReviewToken ??= NewsletterService.NewToken();
            order.ReviewRequestSentAtUtc = DateTime.UtcNow;
            // Marked before sending so a crash after the send can't email the buyer twice.
            await orders.UpdateAsync(order);

            if (string.IsNullOrWhiteSpace(to))
            {
                continue;
            }

            try
            {
                await mailer.SendAsync(await BuildEmailAsync(order, order.ReviewToken), ct);
                sent++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Review request for order {OrderId} failed; will retry", order.Id);
                order.ReviewRequestSentAtUtc = null;
                await orders.UpdateAsync(order);
            }
        }
        return sent;
    }

    public async Task<OutgoingEmail> BuildEmailAsync(Order order, string token, bool reminder = false)
    {
        string to = !string.IsNullOrWhiteSpace(order.User?.Email) ? order.User!.Email : order.GuestEmail ?? string.Empty;
        string link = $"{FrontendUrl}/review/{order.Id}?t={Uri.EscapeDataString(token)}";
        string greeting = string.IsNullOrWhiteSpace(order.User?.FirstName) ? "Hello," : $"Hi {WebUtility.HtmlEncode(order.User!.FirstName)},";
        List<string> names = order.Items.Select(i => i.ProductName).Where(n => !string.IsNullOrWhiteSpace(n)).ToList();
        string piece = names.Count == 1 ? WebUtility.HtmlEncode(names[0]) : "your pieces";

        string intro = reminder
            ? $"<p>Just a gentle nudge, and the only one we'll send: if you have a minute, we'd still love to hear how you're getting on with {piece}.</p><p>A review helps other people buying vintage online feel confident about a small shop.</p>"
            : $"<p>Your Eden Relics order should have reached you by now. How are you getting on with {piece}?</p><p>If you have a minute, we'd love a review. It helps other people buying vintage online feel confident about a small shop, and it tells us what we're getting right.</p>";
        string subject = reminder ? "A quick review of your Eden Relics order?" : "How are you getting on with your order?";

        string body = $"""
            <p>{greeting}</p>
            {intro}
            <p style="text-align:center;margin:28px 0;"><a href="{WebUtility.HtmlEncode(link)}" style="display:inline-block;background:#2E1A0E;color:#F5F0E6;text-decoration:none;padding:12px 28px;font-size:14px;letter-spacing:1px;text-transform:uppercase;">Leave a review</a></p>
            <p>You can add a photo of you wearing it if you'd like. No account needed: the link above is just for your order.</p>
            <p>Thank you,<br>Eden Relics</p>
            """;

        Dictionary<string, string> all = await content.GetAllAsync(null);
        RenderedEmail rendered = EmailTemplate.Render(
            subject,
            "A quick review helps other vintage shoppers find us.",
            body,
            EmailTemplate.FooterFrom(all),
            FrontendUrl,
            unsubscribeUrl: null);

        return new OutgoingEmail(to, subject, rendered.Html, rendered.Text);
    }
}
