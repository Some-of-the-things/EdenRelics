namespace Eden_Relics_BE.DTOs;

public record NewsletterDto(
    Guid Id,
    string Subject,
    string PreviewText,
    string BodyHtml,
    string EditorMode,
    string Status,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    DateTime? ScheduledForUtc,
    DateTime? SendStartedAtUtc,
    DateTime? SentAtUtc,
    int RecipientCount,
    int SentCount,
    int FailedCount,
    int SkippedCount,
    string? LastError);

public record SaveNewsletterRequest(string? Subject, string? PreviewText, string? BodyHtml, string? EditorMode);

public record NewsletterPreviewRequest(string? Subject, string? PreviewText, string? BodyHtml);

/// <param name="BodyHtml">The body after clean-up: what the visual editor should show.</param>
public record NewsletterPreviewDto(string Html, string Text, string BodyHtml);

public record NewsletterTestRequest(string? Email);

/// <param name="ScheduledForUtc">Null or in the past to send now.</param>
public record SendNewsletterRequest(DateTime? ScheduledForUtc);

public record NewsletterAudienceDto(int ActiveCount);

public record AdminSubscriberDto(
    Guid Id,
    string Email,
    string? FirstName,
    string Source,
    DateTime SignedUpAtUtc,
    bool Unsubscribed,
    DateTime? UnsubscribedAtUtc);

public record UnsubscribeTokenRequest(string? Token);
