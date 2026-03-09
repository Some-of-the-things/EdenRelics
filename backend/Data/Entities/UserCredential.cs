namespace Eden_Relics_BE.Data.Entities;

public class UserCredential : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public required byte[] CredentialId { get; set; }
    public required byte[] PublicKey { get; set; }
    public required byte[] UserHandle { get; set; }
    public uint SignatureCounter { get; set; }
    public string CredType { get; set; } = "public-key";
    public Guid AaGuid { get; set; }
    public string? Nickname { get; set; }
}
