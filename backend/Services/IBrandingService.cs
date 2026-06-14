namespace Eden_Relics_BE.Services;

public interface IBrandingService
{
    Task<BrandingDto> GetAsync();
    Task<BrandingDto> UpdateAsync(BrandingDto dto);
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
