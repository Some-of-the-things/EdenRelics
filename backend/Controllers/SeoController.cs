using Eden_Relics_BE.Data.Entities;
using Eden_Relics_BE.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eden_Relics_BE.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public partial class SeoController(ISeoService seo) : ControllerBase
{
    [HttpGet("health/snapshots")]
    public async Task<ActionResult<List<SeoHealthSnapshot>>> GetHealthSnapshots([FromQuery] int take = 30)
    {
        return Ok(await seo.GetHealthSnapshotsAsync(take));
    }

    [HttpGet("health/snapshots/latest")]
    public async Task<ActionResult<SeoHealthSnapshot>> GetLatestHealthSnapshot()
    {
        SeoHealthSnapshot? snapshot = await seo.GetLatestHealthSnapshotAsync();
        return snapshot is null ? NotFound() : Ok(snapshot);
    }

    [HttpPost("health/snapshots/run")]
    public async Task<ActionResult<SeoHealthSnapshot>> RunHealthSnapshot()
    {
        return Ok(await seo.RunHealthSnapshotAsync());
    }

    [HttpPost("analyse")]
    public async Task<ActionResult<SeoAnalysisResult>> Analyse([FromBody] SeoAnalyseRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Url))
        {
            return BadRequest(new { message = "URL is required." });
        }

        // SSRF guard: only fetch public http/https URLs (no internal/loopback/metadata hosts).
        if (!await UrlSafety.IsSafePublicUrlAsync(request.Url))
        {
            return BadRequest(new { message = "URL must be a public http(s) address." });
        }

        SeoAnalysisResult? result = await seo.AnalyseAsync(request.Url);
        return result is null ? BadRequest(new { message = "Could not fetch the URL." }) : Ok(result);
    }

    [HttpGet("keywords")]
    public async Task<ActionResult<List<TrackedKeywordDto>>> GetTrackedKeywords()
    {
        return Ok(await seo.GetTrackedKeywordsAsync());
    }

    [HttpPost("keywords")]
    public async Task<ActionResult<TrackedKeywordDto>> AddKeyword([FromBody] CreateTrackedKeywordDto dto)
    {
        TrackedKeywordDto created = await seo.AddKeywordAsync(dto);
        return CreatedAtAction(nameof(GetTrackedKeywords), null, created);
    }

    [HttpPost("keywords/check-all")]
    public async Task<ActionResult> CheckAllKeywords()
    {
        return Ok(await seo.CheckAllKeywordsAsync());
    }

    [HttpPut("keywords/{id:guid}")]
    public async Task<ActionResult<TrackedKeywordDto>> UpdateKeyword(Guid id, [FromBody] UpdateTrackedKeywordDto dto)
    {
        TrackedKeywordDto? updated = await seo.UpdateKeywordAsync(id, dto);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpDelete("keywords/{id:guid}")]
    public async Task<IActionResult> DeleteKeyword(Guid id)
    {
        return await seo.DeleteKeywordAsync(id) ? NoContent() : NotFound();
    }
}
