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
[Route("api/sellers")]
public class SellersController(ISellerService sellers, IOptions<MarketplaceOptions> marketplace) : ControllerBase
{
    /// <summary>Public / seller-facing endpoints are hidden until the marketplace is switched on.
    /// Admin endpoints stay reachable so the roster can be prepared before launch.</summary>
    private bool GateOpen => marketplace.Value.Enabled;

    private Guid? CurrentUserId()
    {
        string? id = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(id, out Guid parsed) ? parsed : null;
    }

    // ---- Seller-facing (gated) ----

    [Authorize]
    [HttpPost("apply")]
    public async Task<IActionResult> Apply([FromBody] SellerApplicationDto dto)
    {
        if (!GateOpen)
        {
            return NotFound();
        }
        if (string.IsNullOrWhiteSpace(dto.BusinessName))
        {
            return BadRequest(new { error = "Business name is required." });
        }
        if (CurrentUserId() is not Guid userId)
        {
            return Unauthorized();
        }
        return Ok(await sellers.ApplyAsync(userId, dto));
    }

    [Authorize]
    [HttpGet("me")]
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
        SellerDto? seller = await sellers.GetMineAsync(userId);
        return seller is null ? NotFound() : Ok(seller);
    }

    [HttpGet("{slug}")]
    public async Task<IActionResult> Profile(string slug)
    {
        if (!GateOpen)
        {
            return NotFound();
        }
        SellerDto? seller = await sellers.GetPublicBySlugAsync(slug);
        return seller is null ? NotFound() : Ok(seller);
    }

    [HttpGet("{slug}/products")]
    public async Task<IActionResult> ProfileProducts(string slug)
    {
        if (!GateOpen)
        {
            return NotFound();
        }
        return Ok(await sellers.GetPublicProductsAsync(slug));
    }

    // ---- Admin moderation (always available so the roster can be prepared pre-launch) ----

    [Authorize(Roles = Roles.Admin)]
    [HttpGet("admin/all")]
    public async Task<IActionResult> AdminList([FromQuery] string? status)
    {
        SellerApprovalStatus? filter = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse(status, ignoreCase: true, out SellerApprovalStatus parsed))
            {
                return BadRequest(new { error = $"Unknown status '{status}'." });
            }
            filter = parsed;
        }
        return Ok(await sellers.ListAsync(filter));
    }

    [Authorize(Roles = Roles.Admin)]
    [HttpPost("admin/{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id)
    {
        SellerDto? seller = await sellers.SetStatusAsync(id, SellerApprovalStatus.Approved, null);
        return seller is null ? NotFound() : Ok(seller);
    }

    [Authorize(Roles = Roles.Admin)]
    [HttpPost("admin/{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, [FromBody] SellerStatusUpdateDto? body)
    {
        SellerDto? seller = await sellers.SetStatusAsync(id, SellerApprovalStatus.Rejected, body?.Note);
        return seller is null ? NotFound() : Ok(seller);
    }

    [Authorize(Roles = Roles.Admin)]
    [HttpPost("admin/{id:guid}/suspend")]
    public async Task<IActionResult> Suspend(Guid id, [FromBody] SellerStatusUpdateDto? body)
    {
        SellerDto? seller = await sellers.SetStatusAsync(id, SellerApprovalStatus.Suspended, body?.Note);
        return seller is null ? NotFound() : Ok(seller);
    }
}
