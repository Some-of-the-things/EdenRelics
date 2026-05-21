using System.Security.Claims;
using Eden_Relics_BE.Data;
using Eden_Relics_BE.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Eden_Relics_BE.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReviewsController(EdenRelicsDbContext context) : ControllerBase
{
    private const string DeliveredStatus = "Delivered";

    [HttpGet("public")]
    public async Task<ActionResult<IEnumerable<PublicReviewDto>>> GetApproved([FromQuery] int take = 12)
    {
        int limit = Math.Clamp(take, 1, 100);

        List<Review> reviews = await context.Reviews
            .Where(r => r.Status == ReviewStatus.Approved)
            .OrderByDescending(r => r.ModeratedAtUtc ?? r.CreatedAtUtc)
            .Take(limit)
            .ToListAsync();

        return Ok(reviews.Select(ToPublicDto));
    }

    [HttpGet("public/summary")]
    public async Task<ActionResult<ReviewSummaryDto>> GetSummary()
    {
        List<Review> approved = await context.Reviews
            .Where(r => r.Status == ReviewStatus.Approved)
            .Select(r => new Review
            {
                TransactionRating = r.TransactionRating,
                DeliveryRating = r.DeliveryRating,
                ProductRating = r.ProductRating,
            })
            .ToListAsync();

        if (approved.Count == 0)
        {
            return Ok(new ReviewSummaryDto(0, 0, 0, 0, 0));
        }

        double tx = approved.Average(r => (double)r.TransactionRating);
        double dl = approved.Average(r => (double)r.DeliveryRating);
        double pr = approved.Average(r => (double)r.ProductRating);
        double overall = (tx + dl + pr) / 3.0;

        return Ok(new ReviewSummaryDto(
            approved.Count,
            Math.Round(overall, 2),
            Math.Round(tx, 2),
            Math.Round(dl, 2),
            Math.Round(pr, 2)));
    }

    [HttpGet("eligible")]
    [Authorize]
    public async Task<ActionResult<IEnumerable<EligibleOrderDto>>> GetEligibleOrders()
    {
        Guid userId = GetUserId();

        List<Order> orders = await context.Orders
            .Where(o => o.UserId == userId && o.Status == DeliveredStatus)
            .Where(o => !context.Reviews.Any(r => r.OrderId == o.Id))
            .Include(o => o.Items)
            .OrderByDescending(o => o.CreatedAtUtc)
            .ToListAsync();

        return Ok(orders.Select(o => new EligibleOrderDto(
            o.Id,
            o.CreatedAtUtc,
            o.Total,
            o.Items.Select(i => i.ProductName).ToList())));
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<IEnumerable<MyReviewDto>>> GetMine()
    {
        Guid userId = GetUserId();

        List<Review> reviews = await context.Reviews
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.CreatedAtUtc)
            .ToListAsync();

        return Ok(reviews.Select(r => new MyReviewDto(
            r.Id,
            r.OrderId,
            r.TransactionRating,
            r.DeliveryRating,
            r.ProductRating,
            r.Comment,
            r.Status.ToString(),
            r.CreatedAtUtc,
            r.ModerationNote)));
    }

    [HttpPost]
    [Authorize]
    public async Task<ActionResult<MyReviewDto>> Submit(SubmitReviewDto dto)
    {
        Guid userId = GetUserId();

        if (!ValidRating(dto.TransactionRating) || !ValidRating(dto.DeliveryRating) || !ValidRating(dto.ProductRating))
        {
            return BadRequest(new { error = "Ratings must be between 1 and 5." });
        }
        if (string.IsNullOrWhiteSpace(dto.Comment) || dto.Comment.Trim().Length < 10)
        {
            return BadRequest(new { error = "Please write at least 10 characters." });
        }

        Order? order = await context.Orders
            .Include(o => o.User)
            .FirstOrDefaultAsync(o => o.Id == dto.OrderId);

        if (order is null || order.UserId != userId)
        {
            return NotFound();
        }
        if (order.Status != DeliveredStatus)
        {
            return BadRequest(new { error = "You can only review delivered orders." });
        }
        if (await context.Reviews.AnyAsync(r => r.OrderId == order.Id))
        {
            return Conflict(new { error = "A review for this order already exists." });
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

        context.Reviews.Add(review);
        await context.SaveChangesAsync();

        return Ok(new MyReviewDto(
            review.Id,
            review.OrderId,
            review.TransactionRating,
            review.DeliveryRating,
            review.ProductRating,
            review.Comment,
            review.Status.ToString(),
            review.CreatedAtUtc,
            review.ModerationNote));
    }

    [HttpGet("admin")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<IEnumerable<AdminReviewDto>>> GetAdmin([FromQuery] string? status)
    {
        IQueryable<Review> query = context.Reviews
            .Include(r => r.Order)
                .ThenInclude(o => o.Items)
            .Include(r => r.User);

        if (Enum.TryParse(status, true, out ReviewStatus parsed))
        {
            query = query.Where(r => r.Status == parsed);
        }

        List<Review> reviews = await query
            .OrderByDescending(r => r.CreatedAtUtc)
            .ToListAsync();

        return Ok(reviews.Select(ToAdminDto));
    }

    [HttpPost("admin/{id:guid}/approve")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<AdminReviewDto>> Approve(Guid id, [FromBody] ModerateReviewDto? dto)
    {
        Review? review = await context.Reviews
            .Include(r => r.Order)
                .ThenInclude(o => o.Items)
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (review is null)
        {
            return NotFound();
        }

        review.Status = ReviewStatus.Approved;
        review.ModeratedAtUtc = DateTime.UtcNow;
        review.ModeratedByUserId = GetUserId();
        review.ModerationNote = dto?.Note;
        review.UpdatedAtUtc = DateTime.UtcNow;

        await context.SaveChangesAsync();
        return Ok(ToAdminDto(review));
    }

    [HttpPost("admin/{id:guid}/reject")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<AdminReviewDto>> Reject(Guid id, [FromBody] ModerateReviewDto? dto)
    {
        Review? review = await context.Reviews
            .Include(r => r.Order)
                .ThenInclude(o => o.Items)
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (review is null)
        {
            return NotFound();
        }

        review.Status = ReviewStatus.Rejected;
        review.ModeratedAtUtc = DateTime.UtcNow;
        review.ModeratedByUserId = GetUserId();
        review.ModerationNote = dto?.Note;
        review.UpdatedAtUtc = DateTime.UtcNow;

        await context.SaveChangesAsync();
        return Ok(ToAdminDto(review));
    }

    private Guid GetUserId()
    {
        return Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    }

    private static bool ValidRating(int r) => r >= 1 && r <= 5;

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

    private static PublicReviewDto ToPublicDto(Review r)
    {
        return new PublicReviewDto(
            r.Id,
            r.AuthorDisplayName,
            r.TransactionRating,
            r.DeliveryRating,
            r.ProductRating,
            r.Comment,
            r.ModeratedAtUtc ?? r.CreatedAtUtc);
    }

    private static AdminReviewDto ToAdminDto(Review r)
    {
        return new AdminReviewDto(
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
}

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
