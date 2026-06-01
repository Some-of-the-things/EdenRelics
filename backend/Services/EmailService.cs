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

    /// <summary>Notifies the site owner that an order has been paid.</summary>
    Task SendOwnerSaleNotificationAsync(Order order);
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
}
