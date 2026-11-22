using Resend;

namespace Eden_Relics_BE.Services;

public record OutgoingEmail(string To, string Subject, string Html, string Text, Dictionary<string, string>? Headers = null);

/// <summary>A recipient in a batch the provider refused, by its position in the batch.</summary>
public record BatchFailure(int Index, string Message);

/// <summary>Sends newsletter and review-request email. Separate from <see cref="IEmailService"/> so tests can record what was sent.</summary>
public interface INewsletterMailer
{
    Task SendAsync(OutgoingEmail email, CancellationToken ct = default);

    /// <summary>
    /// Sends up to 100 emails in one call. The idempotency key must be the same on a retry of the
    /// same batch, so a batch whose response was lost isn't sent twice. Throws if the whole call
    /// failed; returns the recipients the provider refused individually.
    /// </summary>
    Task<IReadOnlyList<BatchFailure>> SendBatchAsync(string idempotencyKey, IReadOnlyList<OutgoingEmail> emails, CancellationToken ct = default);
}

public class ResendNewsletterMailer(IResend resend, IConfiguration configuration) : INewsletterMailer
{
    private readonly string _from = configuration["Email:From"] ?? "Eden Relics <noreply@edenrelics.com>";

    public async Task SendAsync(OutgoingEmail email, CancellationToken ct = default)
    {
        await resend.EmailSendAsync(ToMessage(email), ct);
    }

    public async Task<IReadOnlyList<BatchFailure>> SendBatchAsync(string idempotencyKey, IReadOnlyList<OutgoingEmail> emails, CancellationToken ct = default)
    {
        var response = await resend.EmailBatchAsync(
            idempotencyKey,
            emails.Select(ToMessage).ToList(),
            EmailBatchValidationMode.Permissive,
            ct);

        if (!response.Success)
        {
            throw (Exception?)response.Exception ?? new InvalidOperationException("The email provider refused the batch.");
        }

        return response.Content?.Errors?.Select(e => new BatchFailure(e.Index, e.Message)).ToList() ?? [];
    }

    private EmailMessage ToMessage(OutgoingEmail email)
    {
        EmailMessage message = new()
        {
            From = _from,
            To = [email.To],
            Subject = email.Subject,
            HtmlBody = email.Html,
            TextBody = email.Text,
        };
        if (email.Headers is not null)
        {
            message.Headers = new Dictionary<string, string>(email.Headers);
        }
        return message;
    }
}
