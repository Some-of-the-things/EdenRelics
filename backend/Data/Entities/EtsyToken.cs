namespace Eden_Relics_BE.Data.Entities;

public class EtsyToken : BaseEntity
{
    public string AccessToken { get; set; } = "";
    public string RefreshToken { get; set; } = "";
    public string ShopId { get; set; } = "";
    public DateTime ExpiresAtUtc { get; set; }
}
