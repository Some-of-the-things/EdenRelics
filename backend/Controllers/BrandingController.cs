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

    // Defaults below define the canonical Eden Relics brand (warm vintage palette,
    // WCAG 2.1 AA). They mirror the :root fallbacks in styles.scss. Used whenever
    // no SiteBranding row exists, so the brand lives in code, not just the DB.
    private static BrandingDto ToDto(SiteBranding? b) => new(
        b?.LogoUrl,
        b?.BgPrimary ?? "#F5F0E6",
        b?.BgSecondary ?? "#EAE0CC",
        b?.BgCard ?? "#FBF8F1",
        b?.BgDark ?? "#1C1510",
        b?.TextPrimary ?? "#2E1A0E",
        b?.TextSecondary ?? "#5C3D1E",
        b?.TextMuted ?? "#6E4A22",
        b?.TextInverse ?? "#F5F0E6",
        b?.Accent ?? "#9B4A1E",
        b?.AccentHover ?? "#7A3A16",
        b?.FontDisplay ?? "Playfair Display",
        b?.FontBody ?? "EB Garamond"
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
