using System.Security.Claims;
using Eden_Relics_BE.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Eden_Relics_BE.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReviewsController(
    IReviewService reviews,
    IReviewRequestService reviewRequests,
    ImageStorageService storage,
    IWebHostEnvironment env,
    ILogger<ReviewsController> logger) : ControllerBase
{
    private const long MaxPhotoBytes = 15L * 1024 * 1024;

    [HttpGet("public")]
    public async Task<ActionResult<IEnumerable<PublicReviewDto>>> GetPublished([FromQuery] int take = 12)
    {
        return Ok(await reviews.GetPublishedAsync(take));
    }

    [HttpGet("public/summary")]
    public async Task<ActionResult<ReviewSummaryDto>> GetSummary()
    {
        return Ok(await reviews.GetSummaryAsync());
    }

    [HttpGet("public/product/{productId:guid}")]
    public async Task<ActionResult<IEnumerable<PublicReviewDto>>> GetForProduct(Guid productId)
    {
        return Ok(await reviews.GetPublishedForProductAsync(productId));
    }

    [HttpGet("eligible")]
    [Authorize]
    public async Task<ActionResult<IEnumerable<EligibleOrderDto>>> GetEligibleOrders()
    {
        return Ok(await reviews.GetEligibleOrdersAsync(RequireUserId()));
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<IEnumerable<MyReviewDto>>> GetMine()
    {
        return Ok(await reviews.GetMineAsync(RequireUserId()));
    }

    /// <summary>The review form's order: from the emailed link (token) or the signed-in buyer.</summary>
    [HttpGet("order/{orderId:guid}")]
    public async Task<ActionResult<ReviewInviteDto>> GetInvite(Guid orderId, [FromQuery] string? t)
    {
        ReviewInviteDto? invite = await reviews.GetInviteAsync(orderId, t, CurrentUserId());
        return invite is null ? NotFound() : Ok(invite);
    }

    [HttpPost("order/{orderId:guid}")]
    [EnableRateLimiting("contact")]
    public async Task<ActionResult<MyReviewDto>> Submit(Guid orderId, [FromBody] SubmitOrderReviewDto dto)
    {
        SubmitReviewResult result = await reviews.SubmitAsync(orderId, dto.Token, CurrentUserId(), dto);
        return result.Outcome switch
        {
            SubmitReviewOutcome.OrderNotFound => NotFound(new { error = "This review link isn't valid." }),
            SubmitReviewOutcome.OrderNotDispatched => BadRequest(new { error = "You can review your order once it has been sent." }),
            SubmitReviewOutcome.AlreadyReviewed => Conflict(new { error = "This order has already been reviewed. Thank you!" }),
            SubmitReviewOutcome.Invalid => BadRequest(new { error = result.Error }),
            _ => Ok(result.Review),
        };
    }

    [HttpPost("order/{orderId:guid}/photo")]
    [EnableRateLimiting("contact")]
    [RequestSizeLimit(MaxPhotoBytes + 1024 * 1024)]
    public async Task<ActionResult<object>> UploadPhoto(Guid orderId, [FromQuery] string? t, IFormFile file)
    {
        if (!await reviews.CanReviewAsync(orderId, t, CurrentUserId()))
        {
            return NotFound(new { error = "This review link isn't valid." });
        }

        string[] allowed = [".jpg", ".jpeg", ".png", ".webp", ".heic", ".heif"];
        string extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!allowed.Contains(extension))
        {
            return BadRequest(new { error = "Please choose a JPG, PNG or WebP photo." });
        }
        if (file.Length == 0 || file.Length > MaxPhotoBytes)
        {
            return BadRequest(new { error = "Please choose a photo under 15 MB." });
        }

        try
        {
            await using Stream stream = file.OpenReadStream();
            string url = await ImageUploadHelper.ProcessAndUploadAsync(stream, storage, env, Request, "reviews", [600, 1200]);
            return Ok(new { url });
        }
        catch (Exception ex) when (ex is SixLabors.ImageSharp.UnknownImageFormatException or SixLabors.ImageSharp.InvalidImageContentException)
        {
            return BadRequest(new { error = "That file doesn't look like a photo we can use. Please try a JPG or PNG." });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Review photo upload failed for order {OrderId}", orderId);
            return StatusCode(500, new { error = "The photo couldn't be uploaded. Please try again." });
        }
    }

    [HttpGet("admin")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<IEnumerable<AdminReviewDto>>> GetAdmin([FromQuery] string? status)
    {
        return Ok(await reviews.GetForAdminAsync(status));
    }

    [HttpPost("admin/{id:guid}/publish")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<AdminReviewDto>> Publish(Guid id)
    {
        AdminReviewDto? review = await reviews.PublishAsync(id, RequireUserId());
        return review is null ? NotFound() : Ok(review);
    }

    [HttpPost("admin/{id:guid}/hide")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<AdminReviewDto>> Hide(Guid id, [FromBody] HideReviewDto dto)
    {
        try
        {
            AdminReviewDto? review = await reviews.HideAsync(id, RequireUserId(), dto.Reason ?? string.Empty, dto.Note);
            return review is null ? NotFound() : Ok(review);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("admin/{id:guid}/reply")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<AdminReviewDto>> Reply(Guid id, [FromBody] ReplyReviewDto dto)
    {
        try
        {
            AdminReviewDto? review = await reviews.ReplyAsync(id, dto.Reply);
            return review is null ? NotFound() : Ok(review);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("admin/settings")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ReviewSettingsDto>> GetSettings()
    {
        return Ok(new ReviewSettingsDto(await reviewRequests.GetDelayDaysAsync(), await reviewRequests.GetReminderDaysAsync()));
    }

    [HttpPut("admin/settings")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ReviewSettingsDto>> SaveSettings([FromBody] ReviewSettingsDto dto)
    {
        try
        {
            if (dto.ReminderDays is < 0 or > ReviewRequestService.MaxReminderDays)
            {
                return BadRequest(new { error = $"Choose a reminder between 0 (none) and {ReviewRequestService.MaxReminderDays} days." });
            }
            await reviewRequests.SetDelayDaysAsync(dto.RequestDelayDays);
            await reviewRequests.SetReminderDaysAsync(dto.ReminderDays);
            return Ok(new ReviewSettingsDto(await reviewRequests.GetDelayDaysAsync(), await reviewRequests.GetReminderDaysAsync()));
        }
        catch (ArgumentOutOfRangeException)
        {
            return BadRequest(new { error = $"Choose between {ReviewRequestService.MinDelayDays} and {ReviewRequestService.MaxDelayDays} days." });
        }
    }

    private Guid? CurrentUserId()
    {
        string? id = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(id, out Guid parsed) ? parsed : null;
    }

    private Guid RequireUserId()
    {
        return CurrentUserId() ?? throw new UnauthorizedAccessException();
    }
}
