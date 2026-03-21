namespace Eden_Relics_BE.Data.Entities;

public class ProductView : BaseEntity
{
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public Guid? UserId { get; set; }
    public string? IpAddress { get; set; }
}
