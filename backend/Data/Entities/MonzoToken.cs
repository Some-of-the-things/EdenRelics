namespace Eden_Relics_BE.Data.Entities;

public class MonzoToken : BaseEntity
{
    public string AccessToken { get; set; } = "";
    public string RefreshToken { get; set; } = "";
    public string AccountId { get; set; } = "";
    public DateTime ExpiresAtUtc { get; set; }
}
