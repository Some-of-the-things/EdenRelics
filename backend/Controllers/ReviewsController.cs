using System.Security.Claims;
using Eden_Relics_BE.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Eden_Relics_BE.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReviewsController(IReviewService reviews) : ControllerBase
{
    [HttpGet("public")]
    public async Task<ActionResult<IEnumerable<PublicReviewDto>>> GetApproved([FromQuery] int take = 12)
    {
        return Ok(await reviews.GetApprovedAsync(take));
    }

    [HttpGet("public/summary")]
    public async Task<ActionResult<ReviewSummaryDto>> GetSummary()
    {
        return Ok(await reviews.GetSummaryAsync());
    }

    [HttpGet("eligible")]
    [Authorize]
    public async Task<ActionResult<IEnumerable<EligibleOrderDto>>> GetEligibleOrders()
    {
        return Ok(await reviews.GetEligibleOrdersAsync(GetUserId()));
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<IEnumerable<MyReviewDto>>> GetMine()
    {
        return Ok(await reviews.GetMineAsync(GetUserId()));
    }

    [HttpPost]
    [Authorize]
    [EnableRateLimiting("contact")]
    public async Task<ActionResult<MyReviewDto>> Submit(SubmitReviewDto dto)
    {
        if (!ValidRating(dto.TransactionRating) || !ValidRating(dto.DeliveryRating) || !ValidRating(dto.ProductRating))
        {
            return BadRequest(new { error = "Ratings must be between 1 and 5." });
        }
        if (string.IsNullOrWhiteSpace(dto.Comment) || dto.Comment.Trim().Length < 10)
        {
            return BadRequest(new { error = "Please write at least 10 characters." });
        }

        SubmitReviewResult result = await reviews.SubmitAsync(GetUserId(), dto);
        return result.Outcome switch
        {
            SubmitReviewOutcome.OrderNotFound => NotFound(),
            SubmitReviewOutcome.OrderNotDelivered => BadRequest(new { error = "You can only review delivered orders." }),
            SubmitReviewOutcome.AlreadyReviewed => Conflict(new { error = "A review for this order already exists." }),
            _ => Ok(result.Review),
        };
    }

    [HttpGet("admin")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<IEnumerable<AdminReviewDto>>> GetAdmin([FromQuery] string? status)
    {
        return Ok(await reviews.GetForAdminAsync(status));
    }

    [HttpPost("admin/{id:guid}/approve")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<AdminReviewDto>> Approve(Guid id, [FromBody] ModerateReviewDto? dto)
    {
        AdminReviewDto? review = await reviews.ApproveAsync(id, GetUserId(), dto?.Note);
        return review is null ? NotFound() : Ok(review);
    }

    [HttpPost("admin/{id:guid}/reject")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<AdminReviewDto>> Reject(Guid id, [FromBody] ModerateReviewDto? dto)
    {
        AdminReviewDto? review = await reviews.RejectAsync(id, GetUserId(), dto?.Note);
        return review is null ? NotFound() : Ok(review);
    }

    private Guid GetUserId()
    {
        return Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    }

    private static bool ValidRating(int r) => r >= 1 && r <= 5;
}
