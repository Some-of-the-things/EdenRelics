using System.Security.Claims;
using Eden_Relics_BE.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eden_Relics_BE.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CareController(ICareService care) : ControllerBase
{
    // --- Public (published only) ---

    [HttpGet("fabric/{slug}")]
    public async Task<ActionResult<CareFabricDto>> GetFabric(string slug)
    {
        CareFabricDto? fabric = await care.GetPublishedFabricAsync(slug);
        return fabric is null ? NotFound() : Ok(fabric);
    }

    [HttpGet("problem/{slug}")]
    public async Task<ActionResult<CareIssueDto>> GetIssue(string slug)
    {
        CareIssueDto? issue = await care.GetPublishedIssueAsync(slug);
        return issue is null ? NotFound() : Ok(issue);
    }

    // --- Admin: worklist ---

    [HttpGet("admin/worklist")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<List<CareWorklistItemDto>>> GetWorklist()
    {
        return Ok(await care.GetWorklistAsync());
    }

    // --- Admin: fabric ---

    [HttpGet("admin/fabric/{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<CareFabricDto>> GetFabricForEdit(Guid id)
    {
        CareFabricDto? fabric = await care.GetFabricAsync(id);
        return fabric is null ? NotFound() : Ok(fabric);
    }

    [HttpPost("admin/fabric")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<CareFabricDto>> CreateFabric([FromBody] SaveCareFabricDto dto)
    {
        return Ok(await care.CreateFabricAsync(dto));
    }

    [HttpPut("admin/fabric/{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<CareFabricDto>> UpdateFabric(Guid id, [FromBody] SaveCareFabricDto dto)
    {
        CareFabricDto? updated = await care.UpdateFabricAsync(id, dto);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpPost("admin/fabric/{id:guid}/publish")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<CareFabricDto>> PublishFabric(Guid id, [FromBody] CarePublishDto dto)
    {
        CareFabricDto? updated = await care.SetFabricPublishedAsync(id, dto.Published, ReviewerName());
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpPost("admin/fabric/{id:guid}/generate")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<CareFabricDto>> GenerateFabricDraft(Guid id)
    {
        if (!care.AiDraftingAvailable)
        {
            return BadRequest(new { error = "AI drafting is not configured (set Anthropic:ApiKey)." });
        }
        CareFabricDto? updated = await care.GenerateFabricDraftAsync(id);
        return updated is null ? NotFound() : Ok(updated);
    }

    // --- Admin: issue ---

    [HttpGet("admin/issue/{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<CareIssueDto>> GetIssueForEdit(Guid id)
    {
        CareIssueDto? issue = await care.GetIssueAsync(id);
        return issue is null ? NotFound() : Ok(issue);
    }

    [HttpPost("admin/issue")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<CareIssueDto>> CreateIssue([FromBody] SaveCareIssueDto dto)
    {
        return Ok(await care.CreateIssueAsync(dto));
    }

    [HttpPut("admin/issue/{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<CareIssueDto>> UpdateIssue(Guid id, [FromBody] SaveCareIssueDto dto)
    {
        CareIssueDto? updated = await care.UpdateIssueAsync(id, dto);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpPost("admin/issue/{id:guid}/publish")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<CareIssueDto>> PublishIssue(Guid id, [FromBody] CarePublishDto dto)
    {
        CareIssueDto? updated = await care.SetIssuePublishedAsync(id, dto.Published, ReviewerName());
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpPost("admin/issue/{id:guid}/generate")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<CareIssueDto>> GenerateIssueDraft(Guid id)
    {
        if (!care.AiDraftingAvailable)
        {
            return BadRequest(new { error = "AI drafting is not configured (set Anthropic:ApiKey)." });
        }
        CareIssueDto? updated = await care.GenerateIssueDraftAsync(id);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpGet("admin/ai-available")]
    [Authorize(Roles = "Admin")]
    public ActionResult<object> AiAvailable()
    {
        return Ok(new { available = care.AiDraftingAvailable });
    }

    private string ReviewerName()
    {
        string? first = User.FindFirstValue(ClaimTypes.GivenName);
        string? last = User.FindFirstValue(ClaimTypes.Surname);
        string full = $"{first} {last}".Trim();
        if (!string.IsNullOrWhiteSpace(full))
        {
            return full;
        }
        return User.FindFirstValue(ClaimTypes.Name)
            ?? User.FindFirstValue(ClaimTypes.Email)
            ?? "Eden Relics";
    }
}

public record CarePublishDto(bool Published);
