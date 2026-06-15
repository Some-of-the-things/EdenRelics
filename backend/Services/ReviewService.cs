using Eden_Relics_BE.Data.Entities;
using Eden_Relics_BE.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Eden_Relics_BE.Services;

public class ReviewService(IRepository<Review> reviews, IRepository<Order> orders) : IReviewService
{
    private const string DeliveredStatus = "Delivered";

    public async Task<List<PublicReviewDto>> GetApprovedAsync(int take)
    {
        int limit = Math.Clamp(take, 1, 100);

        List<Review> approved = await reviews.Query()
            .Where(r => r.Status == ReviewStatus.Approved)
            .OrderByDescending(r => r.ModeratedAtUtc ?? r.CreatedAtUtc)
            .Take(limit)
            .ToListAsync();

        return approved.Select(ToPublicDto).ToList();
    }

    public async Task<ReviewSummaryDto> GetSummaryAsync()
    {
        var ratings = await reviews.Query()
            .Where(r => r.Status == ReviewStatus.Approved)
            .Select(r => new { r.TransactionRating, r.DeliveryRating, r.ProductRating })
            .ToListAsync();

        if (ratings.Count == 0)
        {
            return new ReviewSummaryDto(0, 0, 0, 0, 0);
        }

        double tx = ratings.Average(r => (double)r.TransactionRating);
        double dl = ratings.Average(r => (double)r.DeliveryRating);
        double pr = ratings.Average(r => (double)r.ProductRating);
        double overall = (tx + dl + pr) / 3.0;

        return new ReviewSummaryDto(
            ratings.Count,
            Math.Round(overall, 2),
            Math.Round(tx, 2),
            Math.Round(dl, 2),
            Math.Round(pr, 2));
    }

    public async Task<List<EligibleOrderDto>> GetEligibleOrdersAsync(Guid userId)
    {
        // Delivered orders by this user that don't yet have a review. The subquery
        // over the reviews repository composes because both share the request DbContext.
        List<Order> eligible = await orders.Query()
            .Where(o => o.UserId == userId && o.Status == DeliveredStatus)
            .Where(o => !reviews.Query().Any(r => r.OrderId == o.Id))
            .Include(o => o.Items)
            .OrderByDescending(o => o.CreatedAtUtc)
            .ToListAsync();

        return eligible.Select(o => new EligibleOrderDto(
            o.Id,
            o.CreatedAtUtc,
            o.Total,
            o.Items.Select(i => i.ProductName).ToList())).ToList();
    }

    public async Task<List<MyReviewDto>> GetMineAsync(Guid userId)
    {
        List<Review> mine = await reviews.Query()
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.CreatedAtUtc)
            .ToListAsync();

        return mine.Select(ToMyDto).ToList();
    }

    public async Task<SubmitReviewResult> SubmitAsync(Guid userId, SubmitReviewDto dto)
    {
        Order? order = await orders.Query()
            .Include(o => o.User)
            .FirstOrDefaultAsync(o => o.Id == dto.OrderId);

        if (order is null || order.UserId != userId)
        {
            return new SubmitReviewResult(SubmitReviewOutcome.OrderNotFound, null);
        }
        if (order.Status != DeliveredStatus)
        {
            return new SubmitReviewResult(SubmitReviewOutcome.OrderNotDelivered, null);
        }
        if (await reviews.Query().AnyAsync(r => r.OrderId == order.Id))
        {
            return new SubmitReviewResult(SubmitReviewOutcome.AlreadyReviewed, null);
        }

        string displayName = !string.IsNullOrWhiteSpace(dto.AuthorDisplayName)
            ? dto.AuthorDisplayName.Trim()
            : BuildDisplayName(order.User);

        Review review = new()
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            UserId = userId,
            TransactionRating = dto.TransactionRating,
            DeliveryRating = dto.DeliveryRating,
            ProductRating = dto.ProductRating,
            Comment = dto.Comment.Trim(),
            AuthorDisplayName = TruncateString(displayName, 100),
            Status = ReviewStatus.Pending,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        };

        await reviews.AddAsync(review);
        return new SubmitReviewResult(SubmitReviewOutcome.Success, ToMyDto(review));
    }

    public async Task<List<AdminReviewDto>> GetForAdminAsync(string? status)
    {
        IQueryable<Review> query = reviews.Query()
            .Include(r => r.Order)
                .ThenInclude(o => o.Items)
            .Include(r => r.User);

        if (Enum.TryParse(status, true, out ReviewStatus parsed))
        {
            query = query.Where(r => r.Status == parsed);
        }

        List<Review> list = await query
            .OrderByDescending(r => r.CreatedAtUtc)
            .ToListAsync();

        return list.Select(ToAdminDto).ToList();
    }

    public async Task<AdminReviewDto?> ApproveAsync(Guid id, Guid moderatorId, string? note)
    {
        return await ModerateAsync(id, ReviewStatus.Approved, moderatorId, note);
    }

    public async Task<AdminReviewDto?> RejectAsync(Guid id, Guid moderatorId, string? note)
    {
        return await ModerateAsync(id, ReviewStatus.Rejected, moderatorId, note);
    }

    private async Task<AdminReviewDto?> ModerateAsync(Guid id, ReviewStatus status, Guid moderatorId, string? note)
    {
        Review? review = await reviews.Query()
            .Include(r => r.Order)
                .ThenInclude(o => o.Items)
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (review is null)
        {
            return null;
        }

        review.Status = status;
        review.ModeratedAtUtc = DateTime.UtcNow;
        review.ModeratedByUserId = moderatorId;
        review.ModerationNote = note;
        review.UpdatedAtUtc = DateTime.UtcNow;

        await reviews.UpdateAsync(review);
        return ToAdminDto(review);
    }

    private static string BuildDisplayName(User? user)
    {
        if (user is null)
        {
            return "Customer";
        }
        string first = user.FirstName ?? "";
        string lastInitial = !string.IsNullOrEmpty(user.LastName) ? user.LastName[..1] + "." : "";
        string composed = $"{first} {lastInitial}".Trim();
        return string.IsNullOrWhiteSpace(composed) ? "Customer" : composed;
    }

    private static string TruncateString(string value, int max)
    {
        return value.Length <= max ? value : value[..max];
    }

    private static PublicReviewDto ToPublicDto(Review r) => new(
        r.Id,
        r.AuthorDisplayName,
        r.TransactionRating,
        r.DeliveryRating,
        r.ProductRating,
        r.Comment,
        r.ModeratedAtUtc ?? r.CreatedAtUtc);

    private static MyReviewDto ToMyDto(Review r) => new(
        r.Id,
        r.OrderId,
        r.TransactionRating,
        r.DeliveryRating,
        r.ProductRating,
        r.Comment,
        r.Status.ToString(),
        r.CreatedAtUtc,
        r.ModerationNote);

    private static AdminReviewDto ToAdminDto(Review r) => new(
        r.Id,
        r.OrderId,
        r.UserId,
        r.User?.Email ?? "",
        r.AuthorDisplayName,
        r.TransactionRating,
        r.DeliveryRating,
        r.ProductRating,
        r.Comment,
        r.Status.ToString(),
        r.CreatedAtUtc,
        r.ModeratedAtUtc,
        r.ModerationNote,
        r.Order?.Total ?? 0,
        r.Order?.Items.Select(i => i.ProductName).ToList() ?? []);
}
