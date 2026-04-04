using System.Net;
using Resend;

namespace Eden_Relics_BE.Services;

public interface IEmailService
{
    Task SendVerificationEmailAsync(string toEmail, string firstName, string token);
    Task SendPasswordResetEmailAsync(string toEmail, string firstName, string token);
    Task SendContactEmailAsync(string fromName, string fromEmail, string subject, string message);
    Task SendSaleNotificationAsync(string toEmail, string firstName, string productName, decimal originalPrice, decimal salePrice);
}

public class EmailService : IEmailService
{
    private readonly IResend _resend;
    private readonly string _fromEmail;
    private readonly string _frontendUrl;
    private readonly string _contactRecipient;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IResend resend, IConfiguration configuration, ILogger<EmailService> logger)
    {
        _resend = resend;
        _fromEmail = configuration["Email:From"] ?? "Eden Relics <noreply@edenrelics.com>";
        _frontendUrl = configuration["Email:FrontendUrl"] ?? "http://localhost:4200";
        _contactRecipient = configuration["Email:ContactRecipient"] ?? "edenrelics@dcp-net.com";
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
            var message = new EmailMessage
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
            var message = new EmailMessage
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
            var emailMessage = new EmailMessage
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
            var message = new EmailMessage
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
}
