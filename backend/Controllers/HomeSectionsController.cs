using Eden_Relics_BE.DTOs;
using Eden_Relics_BE.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eden_Relics_BE.Controllers;

/// <summary>
/// The homepage product strips: their wording, order and visibility, and the hand-picked pieces
/// of any custom sections. Edited on the admin Home Sections tab.
/// </summary>
[ApiController]
[Route("api/home-sections")]
[Authorize(Roles = "Admin")]
public class HomeSectionsController(IHomeSectionsService homeSections) : ControllerBase
{
    /// <summary>Public: the visible sections, in display order.</summary>
    [AllowAnonymous]
    [HttpGet]
    public async Task<ActionResult<List<HomeSectionDto>>> Get()
    {
        return Ok(await homeSections.GetPublicAsync());
    }

    /// <summary>Admin: every section, hidden ones included.</summary>
    [HttpGet("admin")]
    public async Task<ActionResult<List<HomeSectionDto>>> GetForAdmin()
    {
        return Ok(await homeSections.GetAdminAsync());
    }

    /// <summary>Admin: replace every section, in display order.</summary>
    [HttpPut("admin")]
    public async Task<ActionResult<List<HomeSectionDto>>> Save([FromBody] SaveHomeSectionsRequest request)
    {
        try
        {
            return Ok(await homeSections.ReplaceAsync(request.Sections ?? []));
        }
        catch (HomeSectionValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
