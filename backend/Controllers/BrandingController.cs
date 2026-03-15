using Eden_Relics_BE.Data;
using Eden_Relics_BE.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace Eden_Relics_BE.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BrandingController(EdenRelicsDbContext context, IWebHostEnvironment env) : ControllerBase
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
            return BadRequest(new { error = "Only image files (jpg, png, webp, svg) are allowed." });

        if (file.Length > 5 * 1024 * 1024)
            return BadRequest(new { error = "File size must be under 5MB." });

        string uploadsDir = Path.Combine(env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot"), "uploads");
        Directory.CreateDirectory(uploadsDir);

        // Save as WebP for the logo
        string logoName = $"logo-{Guid.NewGuid():N}.webp";
        string logoPath = Path.Combine(uploadsDir, logoName);

        if (extension == ".svg")
        {
            // SVG: just save as-is with a different name
            logoName = $"logo-{Guid.NewGuid():N}.svg";
            logoPath = Path.Combine(uploadsDir, logoName);
            await using FileStream fs = new(logoPath, FileMode.Create);
            await file.CopyToAsync(fs);
        }
        else
        {
            using var image = await Image.LoadAsync(file.OpenReadStream());
            image.Mutate(x => x.AutoOrient());

            const int maxHeight = 200;
            if (image.Height > maxHeight)
            {
                image.Mutate(x => x.Resize(0, maxHeight));
            }

            await image.SaveAsync(logoPath, new WebpEncoder { Quality = 85 });
        }

        // Also generate a favicon
        string faviconName = $"favicon-{Guid.NewGuid():N}.png";
        string faviconPath = Path.Combine(uploadsDir, faviconName);

        if (extension != ".svg")
        {
            using var faviconImage = await Image.LoadAsync(file.OpenReadStream());
            faviconImage.Mutate(x => x.AutoOrient().Resize(new ResizeOptions
            {
                Size = new Size(64, 64),
                Mode = ResizeMode.Max
            }));
            await faviconImage.SaveAsPngAsync(faviconPath);
        }

        string logoUrl = $"{Request.Scheme}://{Request.Host}/uploads/{logoName}";
        string? faviconUrl = extension != ".svg"
            ? $"{Request.Scheme}://{Request.Host}/uploads/{faviconName}"
            : null;

        return Ok(new { logoUrl, faviconUrl });
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
