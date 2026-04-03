using Eden_Relics_BE.Data;
using Eden_Relics_BE.Data.Entities;
using Eden_Relics_BE.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Eden_Relics_BE.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BrandingController(EdenRelicsDbContext context, IWebHostEnvironment env, ImageStorageService storage) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<BrandingDto>> Get()
    {
        SiteBranding? branding = await context.SiteBranding.FirstOrDefaultAsync();
        return Ok(ToDto(branding));
    }

    [HttpPut]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<BrandingDto>> Update([FromBody] BrandingDto dto)
    {
        SiteBranding? branding = await context.SiteBranding.FirstOrDefaultAsync();

        if (branding is null)
        {
            branding = new SiteBranding();
            context.SiteBranding.Add(branding);
        }

        branding.LogoUrl = dto.LogoUrl;
        branding.BgPrimary = dto.BgPrimary;
        branding.BgSecondary = dto.BgSecondary;
        branding.BgCard = dto.BgCard;
        branding.BgDark = dto.BgDark;
        branding.TextPrimary = dto.TextPrimary;
        branding.TextSecondary = dto.TextSecondary;
        branding.TextMuted = dto.TextMuted;
        branding.TextInverse = dto.TextInverse;
        branding.Accent = dto.Accent;
        branding.AccentHover = dto.AccentHover;
        branding.FontDisplay = dto.FontDisplay;
        branding.FontBody = dto.FontBody;

        await context.SaveChangesAsync();
        return Ok(ToDto(branding));
    }

    [HttpPost("upload-logo")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<object>> UploadLogo([FromForm] IFormFile file)
    {
        string[] allowedExtensions = [".jpg", ".jpeg", ".png", ".webp", ".svg"];
        string extension = Path.GetExtension(file.FileName).ToLowerInvariant();

        if (!allowedExtensions.Contains(extension))
        {
            return BadRequest(new { error = "Only image files (jpg, png, webp, svg) are allowed." });
        }

        if (file.Length > 5 * 1024 * 1024)
        {
            return BadRequest(new { error = "File size must be under 5MB." });
        }

        string logoUrl = await ImageUploadHelper.ProcessAndUploadAsync(
            file.OpenReadStream(), storage, env, Request, "branding", maxWidth: 400, maxHeight: 200, quality: 85);

        return Ok(new { logoUrl, faviconUrl = (string?)null });
    }

    private static BrandingDto ToDto(SiteBranding? b) => new(
        b?.LogoUrl,
        b?.BgPrimary ?? "#FAF9F7",
        b?.BgSecondary ?? "#F3F1EE",
        b?.BgCard ?? "#FFFFFF",
        b?.BgDark ?? "#2E2E2E",
        b?.TextPrimary ?? "#2E2E2E",
        b?.TextSecondary ?? "#5A5858",
        b?.TextMuted ?? "#706E6C",
        b?.TextInverse ?? "#FAF9F7",
        b?.Accent ?? "#8F1D31",
        b?.AccentHover ?? "#6E1526",
        b?.FontDisplay ?? "Playfair Display",
        b?.FontBody ?? "Work Sans"
    );
}

public record BrandingDto(
    string? LogoUrl,
    string BgPrimary,
    string BgSecondary,
    string BgCard,
    string BgDark,
    string TextPrimary,
    string TextSecondary,
    string TextMuted,
    string TextInverse,
    string Accent,
    string AccentHover,
    string FontDisplay,
    string FontBody
);
