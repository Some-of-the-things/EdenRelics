namespace Eden_Relics_BE.Services;

public interface IReviewService
{
    Task<List<PublicReviewDto>> GetApprovedAsync(int take);
    Task<ReviewSummaryDto> GetSummaryAsync();
    Task<List<EligibleOrderDto>> GetEligibleOrdersAsync(Guid userId);
    Task<List<MyReviewDto>> GetMineAsync(Guid userId);
    Task<SubmitReviewResult> SubmitAsync(Guid userId, SubmitReviewDto dto);
    Task<List<AdminReviewDto>> GetForAdminAsync(string? status);
    Task<AdminReviewDto?> ApproveAsync(Guid id, Guid moderatorId, string? note);
    Task<AdminReviewDto?> RejectAsync(Guid id, Guid moderatorId, string? note);
}

public enum SubmitReviewOutcome { Success, OrderNotFound, OrderNotDelivered, AlreadyReviewed }

public record SubmitReviewResult(SubmitReviewOutcome Outcome, MyReviewDto? Review);

public record SubmitReviewDto(
    Guid OrderId,
    int TransactionRating,
    int DeliveryRating,
    int ProductRating,
    string Comment,
    string? AuthorDisplayName);

public record ModerateReviewDto(string? Note);

public record PublicReviewDto(
    Guid Id,
    string AuthorDisplayName,
    int TransactionRating,
    int DeliveryRating,
    int ProductRating,
    string Comment,
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
    Guid UserId,
    string UserEmail,
    string AuthorDisplayName,
    int TransactionRating,
    int DeliveryRating,
    int ProductRating,
    string Comment,
    string Status,
    DateTime CreatedAtUtc,
    DateTime? ModeratedAtUtc,
    string? ModerationNote,
    decimal OrderTotal,
    List<string> ProductNames);
