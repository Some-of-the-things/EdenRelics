namespace Eden_Relics_BE.Data.Entities;

public class SiteBranding : BaseEntity
{
    public string? LogoUrl { get; set; }
    public string? BgPrimary { get; set; }
    public string? BgSecondary { get; set; }
    public string? BgCard { get; set; }
    public string? BgDark { get; set; }
    public string? TextPrimary { get; set; }
    public string? TextSecondary { get; set; }
    public string? TextMuted { get; set; }
    public string? TextInverse { get; set; }
    public string? Accent { get; set; }
    public string? AccentHover { get; set; }
    public string? FontDisplay { get; set; }
    public string? FontBody { get; set; }
}
