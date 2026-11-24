namespace Eden_Relics_BE.Services;

public interface IReviewService
{
    Task<List<PublicReviewDto>> GetPublishedAsync(int take);
    Task<List<PublicReviewDto>> GetPublishedForProductAsync(Guid productId);
    Task<ReviewSummaryDto> GetSummaryAsync();
    Task<List<EligibleOrderDto>> GetEligibleOrdersAsync(Guid userId);
    Task<List<MyReviewDto>> GetMineAsync(Guid userId);

    /// <summary>The order a review link points at, if the token (or the signed-in buyer) matches it.</summary>
    Task<ReviewInviteDto?> GetInviteAsync(Guid orderId, string? token, Guid? userId);

    Task<SubmitReviewResult> SubmitAsync(Guid orderId, string? token, Guid? userId, SubmitOrderReviewDto dto);

    /// <summary>True if the token (or signed-in buyer) may review this order, for the photo upload.</summary>
    Task<bool> CanReviewAsync(Guid orderId, string? token, Guid? userId);

    Task<List<AdminReviewDto>> GetForAdminAsync(string? status);
    Task<AdminReviewDto?> PublishAsync(Guid id, Guid moderatorId);
    Task<AdminReviewDto?> HideAsync(Guid id, Guid moderatorId, string reason, string? note);
    Task<AdminReviewDto?> ReplyAsync(Guid id, string? reply);
}

public enum SubmitReviewOutcome { Success, OrderNotFound, OrderNotDispatched, AlreadyReviewed, Invalid }

public record SubmitReviewResult(SubmitReviewOutcome Outcome, MyReviewDto? Review, string? Error = null);

/// <param name="NameStyle">"first-initial" (default), "first" or "anonymous".</param>
public record SubmitOrderReviewDto(
    string? Token,
    int Rating,
    string? Comment,
    string? FirstName,
    string? LastName,
    string? NameStyle,
    string? PhotoUrl);

public record HideReviewDto(string? Reason, string? Note);

public record ReplyReviewDto(string? Reply);

/// <param name="ReminderDays">Days after the first email to send one reminder; 0 for no reminder.</param>
public record ReviewSettingsDto(int RequestDelayDays, int ReminderDays = ReviewRequestService.DefaultReminderDays);

public record ReviewInviteItemDto(Guid ProductId, string Name, string? ImageUrl, string? Slug);

public record ReviewInviteDto(
    Guid OrderId,
    DateTime PlacedAtUtc,
    List<ReviewInviteItemDto> Items,
    string? FirstName,
    string? LastName,
    bool AlreadyReviewed);

public record PublicReviewDto(
    Guid Id,
    string AuthorDisplayName,
    int Rating,
    int TransactionRating,
    int DeliveryRating,
    int ProductRating,
    string Comment,
    string? PhotoUrl,
    string? Reply,
    DateTime? RepliedAtUtc,
    List<string> ProductNames,
    DateTime PostedAtUtc);

public record ReviewSummaryDto(
    int Count,
    double Overall,
    double Transaction,
    double Delivery,
    double Product);

public record EligibleOrderDto(
    Guid OrderId,
    DateTime PlacedAtUtc,
    decimal Total,
    List<string> ProductNames);

public record MyReviewDto(
    Guid Id,
    Guid OrderId,
    int Rating,
    int TransactionRating,
    int DeliveryRating,
    int ProductRating,
    string Comment,
    string Status,
    DateTime CreatedAtUtc,
    string? ModerationNote);

public record AdminReviewDto(
    Guid Id,
    Guid OrderId,
    Guid? UserId,
    string UserEmail,
    string AuthorDisplayName,
    int Rating,
    int TransactionRating,
    int DeliveryRating,
    int ProductRating,
    string Comment,
    string? PhotoUrl,
    string Status,
    string? HoldReason,
    string? HiddenReason,
    string? Reply,
    DateTime? RepliedAtUtc,
    DateTime CreatedAtUtc,
    DateTime? ModeratedAtUtc,
    string? ModerationNote,
    decimal OrderTotal,
    List<string> ProductNames);
