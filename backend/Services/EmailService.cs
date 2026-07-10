using System.Net;
using Eden_Relics_BE.Data.Entities;
using Resend;

namespace Eden_Relics_BE.Services;

public interface IEmailService
{
    Task SendVerificationEmailAsync(string toEmail, string firstName, string token);
    Task SendPasswordResetEmailAsync(string toEmail, string firstName, string token);
    Task SendContactEmailAsync(string fromName, string fromEmail, string subject, string message);
    Task SendSaleNotificationAsync(string toEmail, string firstName, string productName, decimal originalPrice, decimal salePrice);
    Task SendReviewRequestEmailAsync(string toEmail, string firstName, Guid orderId);

    /// <summary>Sends a newsletter welcome email containing a first-order discount code. Non-throwing.</summary>
    Task SendDiscountWelcomeEmailAsync(string toEmail, string code);

    /// <summary>Notifies the site owner that an order has been paid.</summary>
    Task SendOwnerSaleNotificationAsync(Order order);

    /// <summary>
    /// Sends the customer an HTML invoice for their order. Throws if delivery fails.
    /// <paramref name="platform"/> names the marketplace for off-website orders; defaults to the website.
    /// </summary>
    Task SendOrderInvoiceEmailAsync(Order order, string? platform = null);

    /// <summary>Renders the invoice/thank-you email HTML for an order without sending it (for admin preview).</summary>
    string BuildOrderInvoiceHtml(Order order, string? platform = null);

    /// <summary>Fires a calendar/operator reminder email to the site owner.</summary>
    Task SendOperatorReminderEmailAsync(string toEmail, string title, string body);
}

public class EmailService : IEmailService
{
    private readonly IResend _resend;
    private readonly string _fromEmail;
    private readonly string _frontendUrl;
    private readonly string _contactRecipient;
    private readonly string _saleNotificationRecipient;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IResend resend, IConfiguration configuration, ILogger<EmailService> logger)
    {
        _resend = resend;
        _fromEmail = configuration["Email:From"] ?? "Eden Relics <noreply@edenrelics.com>";
        _frontendUrl = configuration["Email:FrontendUrl"] ?? "http://localhost:4200";
        _contactRecipient = configuration["Email:ContactRecipient"] ?? "edenrelics@dcp-net.com";
        // Temporary inbox until the edenrelics.co.uk mailboxes are set up.
        _saleNotificationRecipient = configuration["Email:SaleNotificationRecipient"] ?? "orionsaxis@gmail.com";
        _logger = logger;
    }

    public async Task SendVerificationEmailAsync(string toEmail, string firstName, string token)
    {
        string verifyUrl = $"{_frontendUrl}/verify-email?email={Uri.EscapeDataString(toEmail)}&token={Uri.EscapeDataString(token)}";

        string html = $"""
            <div style="font-family: Georgia, serif; max-width: 520px; margin: 0 auto; color: #1a1a1a;">
                <h1 style="font-size: 1.5rem; font-weight: 600; margin-bottom: 1rem;">Verify your email</h1>
                <p>Hi {firstName},</p>
                <p>Thanks for creating an Eden Relics account. Please verify your email address by clicking the button below.</p>
                <a href="{verifyUrl}"
                   style="display: inline-block; background: #1a1a1a; color: #fff; text-decoration: none; padding: 12px 28px; font-size: 0.85rem; letter-spacing: 1px; text-transform: uppercase; margin: 1.5rem 0;">
                    Verify Email
                </a>
                <p style="color: #666; font-size: 0.85rem;">Or copy and paste your token: <strong>{token}</strong></p>
                <p style="color: #666; font-size: 0.85rem;">If you didn't create this account, you can safely ignore this email.</p>
            </div>
            """;

        try
        {
            EmailMessage message = new()
            {
                From = _fromEmail,
                To = [toEmail],
                Subject = "Verify your email — Eden Relics",
                HtmlBody = html
            };
            await _resend.EmailSendAsync(message);
            _logger.LogInformation("Verification email sent to {Email}", toEmail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send verification email to {Email}", toEmail);
            throw;
        }
    }

    public async Task SendDiscountWelcomeEmailAsync(string toEmail, string code)
    {
        string html = $"""
            <div style="font-family: Georgia, serif; max-width: 520px; margin: 0 auto; color: #1a1a1a;">
                <h1 style="font-size: 1.5rem; font-weight: 600; margin-bottom: 1rem;">Welcome to Eden Relics</h1>
                <p>Thanks for joining our newsletter — you'll be first to see new one-of-a-kind vintage pieces as they're found.</p>
                <p>As promised, here's <strong>15% off your first order</strong>. Enter this code at checkout:</p>
                <p style="font-size: 1.6rem; letter-spacing: 3px; font-weight: 600; text-align: center; padding: 14px; border: 1px dashed #9b4a1e; color: #9b4a1e; margin: 1.5rem 0;">{code}</p>
                <p style="color: #666; font-size: 0.85rem;">One code per customer, redeemable on your first order. <a href="{_frontendUrl}/shop" style="color: #9b4a1e;">Browse the collection &rarr;</a></p>
            </div>
            """;

        try
        {
            EmailMessage message = new()
            {
                From = _fromEmail,
                To = [toEmail],
                Subject = "Your 15% welcome code — Eden Relics",
                HtmlBody = html
            };
            await _resend.EmailSendAsync(message);
            _logger.LogInformation("Discount welcome email sent to {Email}", toEmail);
        }
        catch (Exception ex)
        {
            // Non-fatal: the code is also shown in the pop-up, so a failed email must not
            // fail the subscribe request.
            _logger.LogError(ex, "Failed to send discount welcome email to {Email}", toEmail);
        }
    }

    public async Task SendPasswordResetEmailAsync(string toEmail, string firstName, string token)
    {
        string resetUrl = $"{_frontendUrl}/reset-password?email={Uri.EscapeDataString(toEmail)}&token={Uri.EscapeDataString(token)}";

        string html = $"""
            <div style="font-family: Georgia, serif; max-width: 520px; margin: 0 auto; color: #1a1a1a;">
                <h1 style="font-size: 1.5rem; font-weight: 600; margin-bottom: 1rem;">Reset your password</h1>
                <p>Hi {firstName},</p>
                <p>We received a request to reset your password. Click the button below to choose a new one.</p>
                <a href="{resetUrl}"
                   style="display: inline-block; background: #1a1a1a; color: #fff; text-decoration: none; padding: 12px 28px; font-size: 0.85rem; letter-spacing: 1px; text-transform: uppercase; margin: 1.5rem 0;">
                    Reset Password
                </a>
                <p style="color: #666; font-size: 0.85rem;">Or copy and paste your token: <strong>{token}</strong></p>
                <p style="color: #666; font-size: 0.85rem;">This link expires in 1 hour. If you didn't request this, you can safely ignore this email.</p>
            </div>
            """;

        try
        {
            EmailMessage message = new()
            {
                From = _fromEmail,
                To = [toEmail],
                Subject = "Reset your password — Eden Relics",
                HtmlBody = html
            };
            await _resend.EmailSendAsync(message);
            _logger.LogInformation("Password reset email sent to {Email}", toEmail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send password reset email to {Email}", toEmail);
            throw;
        }
    }

    public async Task SendContactEmailAsync(string fromName, string fromEmail, string subject, string message)
    {
        string html = $"""
            <div style="font-family: Georgia, serif; max-width: 520px; margin: 0 auto; color: #1a1a1a;">
                <h1 style="font-size: 1.5rem; font-weight: 600; margin-bottom: 1rem;">New Contact Form Submission</h1>
                <p><strong>From:</strong> {WebUtility.HtmlEncode(fromName)} ({WebUtility.HtmlEncode(fromEmail)})</p>
                <p><strong>Subject:</strong> {WebUtility.HtmlEncode(subject)}</p>
                <hr style="border: none; border-top: 1px solid #ddd; margin: 1.5rem 0;" />
                <p style="white-space: pre-wrap;">{WebUtility.HtmlEncode(message)}</p>
            </div>
            """;

        try
        {
            EmailMessage emailMessage = new()
            {
                From = _fromEmail,
                To = [_contactRecipient],
                ReplyTo = [fromEmail],
                Subject = $"Contact: {subject}",
                HtmlBody = html
            };
            await _resend.EmailSendAsync(emailMessage);
            _logger.LogInformation("Contact email sent from {Email}", fromEmail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send contact email from {Email}", fromEmail);
            throw;
        }
    }

    public async Task SendReviewRequestEmailAsync(string toEmail, string firstName, Guid orderId)
    {
        string reviewUrl = $"{_frontendUrl}/review/{orderId}";

        string html = $"""
            <div style="font-family: Georgia, serif; max-width: 520px; margin: 0 auto; color: #1a1a1a;">
                <h1 style="font-size: 1.5rem; font-weight: 600; margin-bottom: 1rem;">How did we do?</h1>
                <p>Hi {WebUtility.HtmlEncode(firstName)},</p>
                <p>Your Eden Relics order has been delivered. We'd love to hear how the whole experience went — from checkout to delivery to the piece itself.</p>
                <p>Reviews help other shoppers find us and help us improve.</p>
                <a href="{reviewUrl}"
                   style="display: inline-block; background: #1a1a1a; color: #fff; text-decoration: none; padding: 12px 28px; font-size: 0.85rem; letter-spacing: 1px; text-transform: uppercase; margin: 1.5rem 0;">
                    Leave a review
                </a>
                <p style="color: #666; font-size: 0.85rem;">Reviews are moderated before being shown on the site.</p>
            </div>
            """;

        try
        {
            EmailMessage message = new()
            {
                From = _fromEmail,
                To = [toEmail],
                Subject = "How did we do? — Eden Relics",
                HtmlBody = html
            };
            await _resend.EmailSendAsync(message);
            _logger.LogInformation("Review request email sent to {Email} for order {OrderId}", toEmail, orderId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send review request email to {Email}", toEmail);
        }
    }

    public async Task SendSaleNotificationAsync(string toEmail, string firstName, string productName, decimal originalPrice, decimal salePrice)
    {
        int discountPercent = (int)Math.Round((1 - salePrice / originalPrice) * 100);
        string shopUrl = $"{_frontendUrl}";

        string html = $"""
            <div style="font-family: Georgia, serif; max-width: 520px; margin: 0 auto; color: #1a1a1a;">
                <h1 style="font-size: 1.5rem; font-weight: 600; margin-bottom: 1rem;">A dress you love is now on sale!</h1>
                <p>Hi {firstName},</p>
                <p><strong>{productName}</strong> is now <strong>{discountPercent}% off</strong> — reduced from £{originalPrice:F2} to <strong>£{salePrice:F2}</strong>.</p>
                <a href="{shopUrl}"
                   style="display: inline-block; background: #1a1a1a; color: #fff; text-decoration: none; padding: 12px 28px; font-size: 0.85rem; letter-spacing: 1px; text-transform: uppercase; margin: 1.5rem 0;">
                    Shop Now
                </a>
                <p style="color: #666; font-size: 0.85rem;">You're receiving this because you favourited this item on Eden Relics.</p>
            </div>
            """;

        try
        {
            EmailMessage message = new()
            {
                From = _fromEmail,
                To = [toEmail],
                Subject = $"{productName} is now on sale! — Eden Relics",
                HtmlBody = html
            };
            await _resend.EmailSendAsync(message);
            _logger.LogInformation("Sale notification sent to {Email} for {Product}", toEmail, productName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send sale notification to {Email}", toEmail);
        }
    }

    public async Task SendOperatorReminderEmailAsync(string toEmail, string title, string body)
    {
        string adminUrl = $"{_frontendUrl}/admin";
        string html = $"""
            <div style="font-family: Georgia, serif; max-width: 520px; margin: 0 auto; color: #1a1a1a;">
                <h1 style="font-size: 1.5rem; font-weight: 600; margin-bottom: 1rem;">{WebUtility.HtmlEncode(title)}</h1>
                <p style="white-space: pre-wrap;">{WebUtility.HtmlEncode(body)}</p>
                <a href="{adminUrl}"
                   style="display: inline-block; background: #1a1a1a; color: #fff; text-decoration: none; padding: 12px 28px; font-size: 0.85rem; letter-spacing: 1px; text-transform: uppercase; margin: 1.5rem 0;">
                    Open admin calendar
                </a>
                <p style="color: #666; font-size: 0.85rem;">This reminder was scheduled in your Eden Relics admin calendar.</p>
            </div>
            """;

        try
        {
            EmailMessage message = new()
            {
                From = _fromEmail,
                To = [toEmail],
                Subject = $"[Eden Relics reminder] {title}",
                HtmlBody = html
            };
            await _resend.EmailSendAsync(message);
            _logger.LogInformation("Operator reminder email sent to {Email}: {Title}", toEmail, title);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send operator reminder email to {Email}", toEmail);
            throw;
        }
    }

    public async Task SendOwnerSaleNotificationAsync(Order order)
    {
        string buyerEmail = order.User?.Email ?? order.GuestEmail ?? "Unknown";
        string buyerName = order.User is not null
            ? $"{order.User.FirstName} {order.User.LastName}".Trim()
            : "Guest";

        string itemRows = string.Join("", order.Items.Select(i => $"""
            <tr>
                <td style="padding: 6px 12px 6px 0; border-bottom: 1px solid #eee;">{WebUtility.HtmlEncode(i.ProductName)}</td>
                <td style="padding: 6px 0; border-bottom: 1px solid #eee; text-align: right; white-space: nowrap;">{i.Quantity} &times; £{i.UnitPrice:F2}</td>
            </tr>
            """));

        decimal itemsSubtotal = order.Items.Sum(i => i.UnitPrice * i.Quantity);

        string[] addressLines =
        [
            order.ShipAddressLine1 ?? "",
            order.ShipAddressLine2 ?? "",
            order.ShipCity ?? "",
            order.ShipCounty ?? "",
            order.ShipPostcode ?? "",
            order.ShipCountry ?? ""
        ];
        string shippingAddress = string.Join("<br/>",
            addressLines.Where(l => !string.IsNullOrWhiteSpace(l)).Select(WebUtility.HtmlEncode));
        if (string.IsNullOrWhiteSpace(shippingAddress))
        {
            shippingAddress = "<em>No shipping address captured</em>";
        }

        string adminUrl = $"{_frontendUrl}/admin";

        string html = $"""
            <div style="font-family: Georgia, serif; max-width: 560px; margin: 0 auto; color: #1a1a1a;">
                <h1 style="font-size: 1.5rem; font-weight: 600; margin-bottom: 0.5rem;">New sale — order paid</h1>
                <p style="color: #666; font-size: 0.85rem; margin-top: 0;">Order {order.Id}</p>

                <p><strong>Customer:</strong> {WebUtility.HtmlEncode(buyerName)} ({WebUtility.HtmlEncode(buyerEmail)})</p>

                <table style="width: 100%; border-collapse: collapse; margin: 1rem 0;">
                    {itemRows}
                    <tr>
                        <td style="padding: 6px 12px 6px 0; text-align: right; color: #666;">Items subtotal</td>
                        <td style="padding: 6px 0; text-align: right; white-space: nowrap;">£{itemsSubtotal:F2}</td>
                    </tr>
                    <tr>
                        <td style="padding: 6px 12px 6px 0; text-align: right; color: #666;">Shipping ({WebUtility.HtmlEncode(order.ShippingMethod ?? "standard")})</td>
                        <td style="padding: 6px 0; text-align: right; white-space: nowrap;">£{order.ShippingCost:F2}</td>
                    </tr>
                    <tr>
                        <td style="padding: 8px 12px 0 0; text-align: right; font-weight: 600;">Total</td>
                        <td style="padding: 8px 0 0 0; text-align: right; font-weight: 600; white-space: nowrap;">£{order.Total:F2}</td>
                    </tr>
                </table>

                <p style="margin-bottom: 0.25rem;"><strong>Ship to:</strong></p>
                <p style="margin-top: 0;">{shippingAddress}</p>

                <a href="{adminUrl}"
                   style="display: inline-block; background: #1a1a1a; color: #fff; text-decoration: none; padding: 12px 28px; font-size: 0.85rem; letter-spacing: 1px; text-transform: uppercase; margin: 1.5rem 0;">
                    View in admin
                </a>
            </div>
            """;

        try
        {
            EmailMessage message = new()
            {
                From = _fromEmail,
                To = [_saleNotificationRecipient],
                Subject = $"New sale — £{order.Total:F2} — Eden Relics",
                HtmlBody = html
            };
            await _resend.EmailSendAsync(message);
            _logger.LogInformation("Owner sale notification sent for order {OrderId}", order.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send owner sale notification for order {OrderId}", order.Id);
        }
    }

    public async Task SendOrderInvoiceEmailAsync(Order order, string? platform = null)
    {
        string toEmail = order.User?.Email ?? order.GuestEmail
            ?? throw new InvalidOperationException($"Order {order.Id} has no customer email to send an invoice to.");

        string orderNo = OrderRef(order);
        string html = BuildOrderInvoiceHtml(order, platform);

        try
        {
            EmailMessage message = new()
            {
                From = _fromEmail,
                To = [toEmail],
                ReplyTo = [_contactRecipient],
                Subject = $"Thank you for your order — Eden Relics ({orderNo})",
                HtmlBody = html
            };
            await _resend.EmailSendAsync(message);
            _logger.LogInformation("Order invoice email sent to {Email} for order {OrderId}", toEmail, order.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send order invoice email for order {OrderId}", order.Id);
            throw;
        }
    }

    private static string OrderRef(Order order) => $"ER-{order.Id.ToString("N")[..8].ToUpperInvariant()}";

    /// <summary>Renders the customer-facing invoice/thank-you email HTML for an order (no send).</summary>
    public string BuildOrderInvoiceHtml(Order order, string? platform = null)
    {
        // Website orders come straight from checkout; manual sends can name the marketplace.
        string platformLabel = string.IsNullOrWhiteSpace(platform) ? "edenrelics.co.uk" : platform.Trim();

        string? firstName = order.User?.FirstName;
        string greeting = string.IsNullOrWhiteSpace(firstName)
            ? "Thank you so much for your order."
            : $"Thank you so much, {WebUtility.HtmlEncode(firstName.Trim())}.";

        // A short, human-friendly order reference derived from the order id.
        string orderNo = OrderRef(order);
        string orderDate = order.CreatedAtUtc.ToString("d MMMM yyyy", System.Globalization.CultureInfo.InvariantCulture);

        // One table row per line item, then a postage row. Inline styles + align attributes
        // keep the layout intact in Outlook, which ignores flexbox and most <style> rules.
        const string cellLeft = "font-size:13px;color:#4e4540;padding:6px 0;border-bottom:1px solid #ddd0c0;";
        const string cellRight = "font-size:13px;color:#4e4540;padding:6px 0;border-bottom:1px solid #ddd0c0;white-space:nowrap;";
        string itemRows = string.Join("\n        ", order.Items.Select(i =>
            $"<tr><td style=\"{cellLeft}\">{WebUtility.HtmlEncode(i.ProductName)}{(i.Quantity > 1 ? $" &times; {i.Quantity}" : "")}</td><td align=\"right\" style=\"{cellRight}\">£{(i.UnitPrice * i.Quantity):F2}</td></tr>"));
        string postage = order.ShippingCost <= 0 ? "Free" : $"£{order.ShippingCost:F2}";
        itemRows += $"\n        <tr><td style=\"{cellLeft}\">UK Postage</td><td align=\"right\" style=\"{cellRight}\">{postage}</td></tr>";

        // The template below is kept as a literal (no string interpolation) so the CSS
        // braces don't need escaping; dynamic values are swapped in via Replace.
        string html = """
<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="UTF-8" />
<meta name="viewport" content="width=device-width, initial-scale=1.0"/>
<title>Eden Relics — Thank You</title>
<style>
  @import url('https://fonts.googleapis.com/css2?family=Playfair+Display:ital,wght@0,400;0,500;1,400;1,500&family=Libre+Baskerville:ital,wght@0,400;0,700;1,400&family=Jost:wght@300;400;500&display=swap');

  * { margin: 0; padding: 0; box-sizing: border-box; }

  body {
    background: #ede8df;
    font-family: 'Jost', sans-serif;
    color: #3a3530;
    padding: 36px 16px;
  }

  .wrapper {
    max-width: 600px;
    margin: 0 auto;
    background: #f7f2eb;
    border: 1px solid #ddd5c5;
  }

  /* Header */
  .header {
    background: #f7f2eb;
    padding: 36px 40px 24px;
    text-align: center;
    border-bottom: 1px solid #ddd5c5;
  }
  .header-eyebrow {
    font-family: 'Jost', sans-serif;
    font-size: 9px;
    letter-spacing: 4px;
    text-transform: uppercase;
    color: #b87265;
    margin-bottom: 14px;
  }
  .logo-text {
    font-family: 'Playfair Display', serif;
    font-size: 42px;
    font-weight: 400;
    color: #b87265;
    letter-spacing: 1px;
    line-height: 1;
  }
  .logo-sub {
    font-family: 'Jost', sans-serif;
    font-size: 9px;
    letter-spacing: 4px;
    text-transform: uppercase;
    color: #a09080;
    margin-top: 8px;
  }
  .header-rule {
    width: 60px;
    height: 1px;
    background: #c9bfb0;
    margin: 16px auto 0;
  }

  /* Sections */
  .section {
    padding: 28px 40px;
  }
  .section + .section {
    border-top: 1px solid #e4dbd0;
  }

  .greeting {
    font-family: 'Playfair Display', serif;
    font-size: 22px;
    font-style: italic;
    font-weight: 400;
    color: #3a3530;
    margin-bottom: 14px;
  }

  p {
    font-size: 14px;
    line-height: 1.85;
    color: #4e4540;
    margin-bottom: 10px;
  }
  p:last-child { margin-bottom: 0; }

  /* Ornament */
  .ornament {
    text-align: center;
    color: #b87265;
    font-size: 14px;
    letter-spacing: 10px;
    padding: 4px 0 16px;
  }

  /* Invoice */
  .invoice-wrap {
    background: #efe8de;
    border-left: 3px solid #b87265;
    padding: 20px 24px;
    margin: 4px 0;
  }
  .invoice-label {
    font-family: 'Jost', sans-serif;
    font-size: 9px;
    letter-spacing: 3px;
    text-transform: uppercase;
    color: #b87265;
    margin-bottom: 16px;
    font-weight: 500;
  }
  .invoice-row {
    display: flex;
    justify-content: space-between;
    align-items: baseline;
    font-size: 13px;
    color: #4e4540;
    padding: 6px 0;
    border-bottom: 1px solid #ddd0c0;
    gap: 12px;
  }
  .invoice-row:last-child { border-bottom: none; }
  .invoice-row.total {
    font-family: 'Playfair Display', serif;
    font-size: 17px;
    color: #3a3530;
    padding-top: 12px;
    margin-top: 4px;
    border-top: 1px solid #b87265;
    border-bottom: none;
    font-weight: 500;
  }
  .invoice-row span:first-child { font-weight: 300; color: #6e6058; }
  .invoice-note {
    font-size: 11px;
    color: #9a8878;
    margin-top: 12px;
    font-style: italic;
    font-family: 'Libre Baskerville', serif;
  }

  /* Care note */
  .care-box {
    border: 1px solid #d4c8b8;
    padding: 16px 20px;
    background: #faf6f0;
    margin: 4px 0;
  }
  .care-box .care-title {
    font-family: 'Playfair Display', serif;
    font-size: 14px;
    font-style: italic;
    color: #3a3530;
    margin-bottom: 6px;
    display: block;
  }
  .care-box p {
    font-size: 13px;
    color: #5e5248;
    margin: 0;
  }

  /* Social */
  .social-heading {
    font-family: 'Playfair Display', serif;
    font-size: 21px;
    font-style: italic;
    color: #3a3530;
    margin-bottom: 12px;
  }
  .platform-list {
    list-style: none;
    display: flex;
    flex-direction: column;
    gap: 7px;
    margin-bottom: 14px;
  }
  .platform-list li {
    display: flex;
    align-items: center;
    gap: 12px;
    font-size: 13px;
    color: #4e4540;
  }
  .platform-pill {
    background: #1a1a1a;
    color: #d4b896;
    font-family: 'Jost', sans-serif;
    font-size: 9px;
    letter-spacing: 2px;
    text-transform: uppercase;
    padding: 4px 10px;
    min-width: 68px;
    text-align: center;
    font-weight: 500;
  }

  /* Review CTA */
  .review-panel {
    background: #1a1a1a;
    padding: 28px 32px;
    text-align: center;
  }
  .review-panel .review-eyebrow {
    font-family: 'Jost', sans-serif;
    font-size: 9px;
    letter-spacing: 3px;
    text-transform: uppercase;
    color: #7a6a5a;
    margin-bottom: 10px;
  }
  .review-panel .review-heading {
    font-family: 'Playfair Display', serif;
    font-size: 20px;
    font-style: italic;
    color: #f7f2eb;
    margin-bottom: 12px;
    font-weight: 400;
  }
  .review-panel p {
    color: #a09080;
    font-size: 13px;
    margin-bottom: 20px;
    line-height: 1.7;
  }
  .review-btn {
    display: inline-block;
    background: #b87265;
    color: #f7f2eb;
    font-family: 'Jost', sans-serif;
    font-size: 10px;
    letter-spacing: 3px;
    text-transform: uppercase;
    padding: 13px 32px;
    text-decoration: none;
    font-weight: 500;
    cursor: pointer;
  }
  .review-google-note {
    margin-top: 14px;
    font-size: 12px;
    color: #6a5a4a;
    font-style: italic;
    font-family: 'Libre Baskerville', serif;
  }

  /* Signoff */
  .signoff-name {
    font-family: 'Playfair Display', serif;
    font-size: 22px;
    font-style: italic;
    color: #3a3530;
    margin-top: 8px;
  }
  .signoff-co {
    font-size: 11px;
    color: #9a8878;
    margin-top: 6px;
    letter-spacing: 1px;
  }

  /* Footer */
  .footer {
    background: #1a1a1a;
    padding: 22px 40px;
    text-align: center;
  }
  .footer p {
    font-size: 11px;
    color: #6a5a4a;
    letter-spacing: 1px;
    margin-bottom: 4px;
  }
  .footer a { color: #b87265; text-decoration: none; }
  .footer .footer-tagline {
    font-family: 'Playfair Display', serif;
    font-size: 13px;
    font-style: italic;
    color: #5a4a3a;
    margin-top: 12px;
    letter-spacing: 1px;
  }
</style>
</head>
<body>
<table role="presentation" width="100%" cellpadding="0" cellspacing="0" bgcolor="#ede8df" style="background:#ede8df;width:100%;margin:0;padding:0;">
<tr><td align="center" style="padding:36px 16px;">
<table role="presentation" width="600" cellpadding="0" cellspacing="0" bgcolor="#f7f2eb" class="wrapper" style="background:#f7f2eb;border:1px solid #ddd5c5;width:600px;max-width:600px;">
<tr><td>

  <!-- Header / Logo -->
  <div class="header">
    <div class="header-eyebrow">Order Confirmation</div>
    <div class="logo-text">EdenRelics</div>
    <div class="logo-sub">Curated Vintage &amp; Timeless Pieces</div>
    <div class="header-rule"></div>
  </div>

  <!-- Thank you -->
  <div class="section">
    <div class="ornament">· · ✦ · ·</div>
    <p class="greeting">@@GREETING@@</p>
    <p>Your order has been received and is being carefully prepared for dispatch. Every piece that leaves us is packed with as much care as we put into finding it — we hope it brings you real joy to wear.</p>
    <p>You'll receive a dispatch notification with tracking details shortly.</p>
  </div>

  <!-- Invoice -->
  <div class="section">
    <div class="invoice-wrap">
      <div class="invoice-label">✦ Your Order Summary</div>
      <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="width:100%;border-collapse:collapse;">
        <tr>
          <td style="font-size:13px;font-weight:300;color:#6e6058;padding:6px 0;border-bottom:1px solid #ddd0c0;">Order #</td>
          <td align="right" style="font-size:13px;color:#4e4540;padding:6px 0;border-bottom:1px solid #ddd0c0;">@@ORDER_NUMBER@@</td>
        </tr>
        <tr>
          <td style="font-size:13px;font-weight:300;color:#6e6058;padding:6px 0;border-bottom:1px solid #ddd0c0;">Date</td>
          <td align="right" style="font-size:13px;color:#4e4540;padding:6px 0;border-bottom:1px solid #ddd0c0;">@@DATE@@</td>
        </tr>
        <tr>
          <td style="font-size:13px;font-weight:300;color:#6e6058;padding:6px 0;border-bottom:1px solid #ddd0c0;">Platform</td>
          <td align="right" style="font-size:13px;color:#4e4540;padding:6px 0;border-bottom:1px solid #ddd0c0;">@@PLATFORM@@</td>
        </tr>
        @@ITEM_ROWS@@
        <tr>
          <td style="font-family:'Playfair Display',Georgia,serif;font-size:17px;font-weight:500;color:#3a3530;padding-top:12px;border-top:1px solid #b87265;">Total Paid</td>
          <td align="right" style="font-family:'Playfair Display',Georgia,serif;font-size:17px;font-weight:500;color:#3a3530;padding-top:12px;border-top:1px solid #b87265;">£@@TOTAL@@</td>
        </tr>
      </table>
      <p class="invoice-note">Returns accepted within 14 days. Please visit edenrelics.co.uk/returns-policy for full details.</p>
    </div>
  </div>

  <!-- Care note -->
  <div class="section">
    <div class="care-box">
      <span class="care-title">✦ A Note on Care</span>
      <p>Vintage pieces are best loved gently. We recommend hand washing in cool water unless otherwise noted. Avoid heat on velvet, silk, or embellishments. If in doubt, do reach out — we're always happy to advise.</p>
    </div>
  </div>

  <!-- Social shoutout -->
  <div class="section">
    <p class="social-heading">Come find us.</p>
    <p>We'd love to stay connected. Follow Eden Relics for new arrivals, behind-the-scenes curation, and the stories behind the pieces.</p>
    <table role="presentation" cellpadding="0" cellspacing="0" style="border-collapse:collapse;margin-bottom:14px;">
      <tr>
        <td style="padding:4px 12px 4px 0;"><span style="background:#1a1a1a;color:#d4b896;font-family:'Jost',Arial,sans-serif;font-size:9px;letter-spacing:2px;text-transform:uppercase;padding:4px 10px;font-weight:500;">Etsy</span></td>
        <td style="font-size:13px;color:#4e4540;padding:4px 0;">etsy.com/shop/EdenRelicsNorwich</td>
      </tr>
      <tr>
        <td style="padding:4px 12px 4px 0;"><span style="background:#1a1a1a;color:#d4b896;font-family:'Jost',Arial,sans-serif;font-size:9px;letter-spacing:2px;text-transform:uppercase;padding:4px 10px;font-weight:500;">Depop</span></td>
        <td style="font-size:13px;color:#4e4540;padding:4px 0;">@edenrelics</td>
      </tr>
      <tr>
        <td style="padding:4px 12px 4px 0;"><span style="background:#1a1a1a;color:#d4b896;font-family:'Jost',Arial,sans-serif;font-size:9px;letter-spacing:2px;text-transform:uppercase;padding:4px 10px;font-weight:500;">Vinted</span></td>
        <td style="font-size:13px;color:#4e4540;padding:4px 0;">@edenrelics</td>
      </tr>
      <tr>
        <td style="padding:4px 12px 4px 0;"><span style="background:#1a1a1a;color:#d4b896;font-family:'Jost',Arial,sans-serif;font-size:9px;letter-spacing:2px;text-transform:uppercase;padding:4px 10px;font-weight:500;">eBay</span></td>
        <td style="font-size:13px;color:#4e4540;padding:4px 0;">edenrelics</td>
      </tr>
      <tr>
        <td style="padding:4px 12px 4px 0;"><span style="background:#1a1a1a;color:#d4b896;font-family:'Jost',Arial,sans-serif;font-size:9px;letter-spacing:2px;text-transform:uppercase;padding:4px 10px;font-weight:500;">Web</span></td>
        <td style="font-size:13px;color:#4e4540;padding:4px 0;">edenrelics.co.uk</td>
      </tr>
      <tr>
        <td style="padding:4px 12px 4px 0;"><span style="background:#1a1a1a;color:#d4b896;font-family:'Jost',Arial,sans-serif;font-size:9px;letter-spacing:2px;text-transform:uppercase;padding:4px 10px;font-weight:500;">Instagram</span></td>
        <td style="font-size:13px;color:#4e4540;padding:4px 0;">@edenrelics</td>
      </tr>
    </table>
    <p style="font-size:13px; font-style:italic; color:#9a8878; font-family:'Libre Baskerville', serif;">Tag us when your piece arrives — we always love to see where our relics end up.</p>
  </div>

  <!-- Review request -->
  <div class="section" style="padding-bottom:0; padding-top:0;">
    <table role="presentation" width="100%" cellpadding="0" cellspacing="0" bgcolor="#1a1a1a" style="background:#1a1a1a;width:100%;">
      <tr><td style="padding:28px 32px;text-align:center;">
        <div style="font-family:'Jost',Arial,sans-serif;font-size:9px;letter-spacing:3px;text-transform:uppercase;color:#7a6a5a;margin-bottom:10px;">A small favour</div>
        <div style="font-family:'Playfair Display',Georgia,serif;font-size:20px;font-style:italic;color:#f7f2eb;margin-bottom:12px;">Would you leave us a review?</div>
        <p style="color:#a09080;font-size:13px;line-height:1.7;margin-bottom:20px;">Reviews make a genuine difference to a small independent shop. If you're happy with your purchase, even a few words goes a long way — it helps other lovers of vintage find us.</p>
        <a href="https://maps.app.goo.gl/jG5ku2piUUzWn32w8?g_st=ic" style="display:inline-block;background:#b87265;color:#f7f2eb;font-family:'Jost',Arial,sans-serif;font-size:10px;letter-spacing:3px;text-transform:uppercase;padding:13px 32px;text-decoration:none;font-weight:500;">Leave a Google Review →</a>
        <p style="margin-top:14px;font-size:12px;color:#a09080;font-style:italic;font-family:'Libre Baskerville',Georgia,serif;">Or search <strong style="color:#9a8878;">Eden Relics Norwich</strong> on Google Maps — every review is read and truly appreciated.</p>
      </td></tr>
    </table>
  </div>

  <!-- Sign-off -->
  <div class="section">
    <p>With warmth,</p>
    <div class="signoff-name">Teodora &amp; the Eden Relics team</div>
    <p style="font-size:12px; color:#9a8878; margin-top:6px;">@@CONTACT_EMAIL@@</p>
  </div>

  <!-- Footer -->
  <table role="presentation" width="100%" cellpadding="0" cellspacing="0" bgcolor="#1a1a1a" style="background:#1a1a1a;width:100%;">
    <tr><td style="padding:22px 40px;text-align:center;">
      <p style="font-size:11px;color:#6a5a4a;letter-spacing:1px;margin-bottom:4px;">Questions? <a href="mailto:@@CONTACT_EMAIL@@" style="color:#b87265;text-decoration:none;">@@CONTACT_EMAIL@@</a></p>
      <p style="font-size:11px;color:#6a5a4a;letter-spacing:1px;margin-bottom:4px;"><a href="https://edenrelics.co.uk" style="color:#b87265;text-decoration:none;">edenrelics.co.uk</a></p>
      <div style="font-family:'Playfair Display',Georgia,serif;font-size:13px;font-style:italic;color:#5a4a3a;margin-top:12px;letter-spacing:1px;">Authentically Vintage · Curated With Intent</div>
    </td></tr>
  </table>

</td></tr>
</table>
</td></tr>
</table>
</body>
</html>
"""
            .Replace("@@GREETING@@", greeting)
            .Replace("@@ORDER_NUMBER@@", orderNo)
            .Replace("@@DATE@@", orderDate)
            .Replace("@@PLATFORM@@", WebUtility.HtmlEncode(platformLabel))
            .Replace("@@ITEM_ROWS@@", itemRows)
            .Replace("@@TOTAL@@", order.Total.ToString("F2"))
            .Replace("@@CONTACT_EMAIL@@", _contactRecipient);

        return html;
    }
}
