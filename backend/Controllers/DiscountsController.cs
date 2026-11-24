using System.Security.Claims;
using Eden_Relics_BE.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Eden_Relics_BE.Controllers;

/// <summary>The newsletter sign-up offer: what the site shows, checking a code in the basket, and the admin settings.</summary>
[ApiController]
[Route("api/discounts")]
public class DiscountsController(ISignupOfferService offers) : ControllerBase
{
    /// <summary>What the sign-up boxes say. Public.</summary>
    [HttpGet("signup-offer")]
    public async Task<ActionResult<PublicSignupOfferDto>> SignupOffer()
    {
        return Ok(await offers.GetPublicOfferAsync());
    }

    /// <summary>Checks a code against the basket so the saving can be shown before paying. The order is checked again when it's created.</summary>
    [HttpPost("check")]
    [EnableRateLimiting("public-write")]
    public async Task<ActionResult<DiscountCheckResult>> Check([FromBody] DiscountCheckRequest request)
    {
        string? signedInEmail = User.Identity?.IsAuthenticated == true ? User.FindFirstValue(ClaimTypes.Email) : null;
        return Ok(await offers.CheckAsync(request, signedInEmail));
    }

    [HttpGet("admin/settings")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<SignupOfferSettings>> GetSettings()
    {
        return Ok(await offers.GetSettingsAsync());
    }

    [HttpPut("admin/settings")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<SignupOfferSettings>> SaveSettings([FromBody] SignupOfferSettings settings)
    {
        try
        {
            return Ok(await offers.SaveSettingsAsync(settings));
        }
        catch (SignupOfferValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("admin/codes")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<AdminDiscountCodesDto>> Codes([FromQuery] string? status)
    {
        return Ok(await offers.GetCodesForAdminAsync(status));
    }
}
