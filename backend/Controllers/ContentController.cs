using Eden_Relics_BE.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eden_Relics_BE.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ContentController(IContentService content) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<Dictionary<string, string>>> GetAll([FromQuery] string? locale = null)
    {
        return Ok(await content.GetAllAsync(locale));
    }

    [HttpPut]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<Dictionary<string, string>>> UpdateAll([FromBody] Dictionary<string, string> body)
    {
        return Ok(await content.UpdateAllAsync(body));
    }
}
