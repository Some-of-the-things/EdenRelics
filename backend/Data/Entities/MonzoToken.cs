namespace Eden_Relics_BE.Data.Entities;

// Rotated on reconnect (old tokens replaced wholesale), so genuinely hard-deleted.
public class MonzoToken : BaseEntity, IHardDeletable
{
    public string AccessToken { get; set; } = "";
    public string RefreshToken { get; set; } = "";
    public string AccountId { get; set; } = "";
    public DateTime ExpiresAtUtc { get; set; }
}
