using Eden_Relics_BE.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eden_Relics_BE.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BrandingController(IBrandingService branding, IWebHostEnvironment env, ImageStorageService storage) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<BrandingDto>> Get()
    {
        return Ok(await branding.GetAsync());
    }

    [HttpPut]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<BrandingDto>> Update([FromBody] BrandingDto dto)
    {
        return Ok(await branding.UpdateAsync(dto));
    }

    [HttpPost("upload-logo")]
    [Authorize(Roles = "Admin")]
    [RequestSizeLimit(long.MaxValue)]
    public async Task<ActionResult<object>> UploadLogo([FromForm] IFormFile file)
    {
        string[] allowedExtensions = [".jpg", ".jpeg", ".png", ".webp", ".svg"];
        string extension = Path.GetExtension(file.FileName).ToLowerInvariant();

        if (!allowedExtensions.Contains(extension))
        {
            return BadRequest(new { error = "Only image files (jpg, png, webp, svg) are allowed." });
        }

        if (file.Length > UploadLimits.MaxUploadBytes)
        {
            return BadRequest(new { error = $"File size must be under {UploadLimits.MaxUploadDisplay}." });
        }

        string logoUrl = await ImageUploadHelper.ProcessAndUploadSingleAsync(
            file.OpenReadStream(), storage, env, Request, "branding", maxWidth: 400, maxHeight: 200, quality: 85);

        return Ok(new { logoUrl, faviconUrl = (string?)null });
    }
}
