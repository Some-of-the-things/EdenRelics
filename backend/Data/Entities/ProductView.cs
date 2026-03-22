namespace Eden_Relics_BE.Data.Entities;

public class ProductView : BaseEntity
{
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public Guid? UserId { get; set; }
    public string? IpAddress { get; set; }
    public string? ReferrerUrl { get; set; }
    public string? UtmSource { get; set; }
    public string? UtmMedium { get; set; }
    public string? UtmCampaign { get; set; }
    public string? Channel { get; set; }
    public string? Country { get; set; }
    public string? UserAgent { get; set; }
}
