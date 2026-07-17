using Eden_Relics_BE.DTOs;
using Eden_Relics_BE.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eden_Relics_BE.Controllers;

/// <summary>
/// The curated "Our Top Picks" edit. The public GET exposes the gate flag and (when live) the
/// curated SKUs for the homepage strip and /top-picks page. The admin endpoints let an admin
/// curate the list ahead of — and independently of — switching the gate on. Gated by
/// <see cref="TopPicksOptions"/>, which is separate from the marketplace switch.
/// </summary>
[ApiController]
[Route("api/top-picks")]
[Authorize(Roles = "Admin")]
public class TopPicksController(ITopPicksService topPicks) : ControllerBase
{
    /// <summary>Public: the gate flag plus the curated SKUs (empty while gated). Drives the
    /// homepage strip, the /top-picks page and the nav link.</summary>
    [AllowAnonymous]
    [HttpGet]
    public async Task<ActionResult<TopPicksPublicDto>> Get()
    {
        return Ok(await topPicks.GetPublicAsync());
    }

    /// <summary>Admin: the full curated list (regardless of the gate) plus the current flag.</summary>
    [HttpGet("admin")]
    public async Task<ActionResult<TopPicksAdminDto>> GetForAdmin()
    {
        return Ok(await topPicks.GetAdminAsync());
    }

    /// <summary>Admin: replace the whole curated list, in display order.</summary>
    [HttpPut("admin")]
    public async Task<ActionResult<TopPicksAdminDto>> Save([FromBody] SaveTopPicksRequest request)
    {
        return Ok(await topPicks.ReplaceAsync(request.Items ?? []));
    }
}
