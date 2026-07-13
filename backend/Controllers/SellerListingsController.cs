using System.Security.Claims;
using Eden_Relics_BE.Auth;
using Eden_Relics_BE.Data.Entities;
using Eden_Relics_BE.DTOs;
using Eden_Relics_BE.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Eden_Relics_BE.Controllers;

[ApiController]
[Route("api/seller-listings")]
public class SellerListingsController(
    ISellerListingService listings,
    IOptions<MarketplaceOptions> marketplace) : ControllerBase
{
    private bool GateOpen => marketplace.Value.Enabled;

    private Guid? CurrentUserId()
    {
        string? id = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(id, out Guid parsed) ? parsed : null;
    }

    // ---- Seller-facing (gated) ----

    [Authorize(Roles = Roles.Seller)]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SellerListingCreateDto dto)
    {
        if (!GateOpen)
        {
            return NotFound();
        }
        if (string.IsNullOrWhiteSpace(dto.Name) || string.IsNullOrWhiteSpace(dto.ImageUrl))
        {
            return BadRequest(new { error = "Name and image are required." });
        }
        if (CurrentUserId() is not Guid userId)
        {
            return Unauthorized();
        }
        SellerListingDto? created = await listings.CreateAsync(userId, dto);
        return created is null
            ? StatusCode(StatusCodes.Status403Forbidden, new { error = "Not an approved seller." })
            : Ok(created);
    }

    [Authorize(Roles = Roles.Seller)]
    [HttpGet("mine")]
    public async Task<IActionResult> Mine()
    {
        if (!GateOpen)
        {
            return NotFound();
        }
        if (CurrentUserId() is not Guid userId)
        {
            return Unauthorized();
        }
        return Ok(await listings.ListMineAsync(userId));
    }

    // ---- Admin moderation (always available so listings can be reviewed pre-launch) ----

    [Authorize(Roles = Roles.Admin)]
    [HttpGet("admin")]
    public async Task<IActionResult> Queue([FromQuery] string? status)
    {
        ProductModerationStatus filter = ProductModerationStatus.PendingReview;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse(status, ignoreCase: true, out ProductModerationStatus parsed))
            {
                return BadRequest(new { error = $"Unknown status '{status}'." });
            }
            filter = parsed;
        }
        return Ok(await listings.ListForModerationAsync(filter));
    }

    [Authorize(Roles = Roles.Admin)]
    [HttpPost("admin/{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id)
    {
        SellerListingDto? result = await listings.ApproveAsync(id);
        return result is null ? NotFound() : Ok(result);
    }

    [Authorize(Roles = Roles.Admin)]
    [HttpPost("admin/{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, [FromBody] ModerationDecisionDto? body)
    {
        SellerListingDto? result = await listings.RejectAsync(id, body?.Note);
        return result is null ? NotFound() : Ok(result);
    }
}
