using Eden_Relics_BE.Services.CrossListing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eden_Relics_BE.Controllers;

/// <summary>
/// Cross-listing readiness for the shop's own stock. Admin-only: we are user zero while the seller
/// beta is gated (crosslisting brief §10 — every feature gets used on real Eden Relics stock first).
/// </summary>
[ApiController]
[Route("api/cross-listing")]
[Authorize(Roles = "Admin")]
public class CrossListingController(ICrossListingService crossListing) : ControllerBase
{
    /// <summary>What each platform would do with this piece right now, and what stops it.</summary>
    [HttpGet("preview/{productId:guid}")]
    public async Task<ActionResult<CrossListingPreview>> Preview(Guid productId)
    {
        CrossListingPreview? preview = await crossListing.PreviewAsync(productId);
        return preview is null ? NotFound() : Ok(preview);
    }

    /// <summary>
    /// Pieces old enough to consider relisting. Decision support, not automation: the response is a
    /// list to choose from one at a time, and never includes anything under the 60-day floor.
    /// </summary>
    [HttpGet("relist-candidates")]
    public async Task<ActionResult<IReadOnlyList<RelistCandidate>>> RelistCandidates()
    {
        return Ok(await crossListing.RelistCandidatesAsync());
    }
}
