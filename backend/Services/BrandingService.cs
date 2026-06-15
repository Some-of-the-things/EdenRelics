using Eden_Relics_BE.Data.Entities;
using Eden_Relics_BE.Repositories;

namespace Eden_Relics_BE.Services;

public class BrandingService(IRepository<SiteBranding> repository) : IBrandingService
{
    public async Task<BrandingDto> GetAsync()
    {
        SiteBranding? branding = (await repository.GetAllAsync()).FirstOrDefault();
        return ToDto(branding);
    }

    public async Task<BrandingDto> UpdateAsync(BrandingDto dto)
    {
        SiteBranding? branding = (await repository.GetAllAsync()).FirstOrDefault();

        bool isNew = branding is null;
        branding ??= new SiteBranding();

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

        if (isNew)
        {
            await repository.AddAsync(branding);
        }
        else
        {
            await repository.UpdateAsync(branding);
        }

        return ToDto(branding);
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
