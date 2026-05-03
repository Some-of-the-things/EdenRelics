namespace Eden_Relics_BE.Data.Entities;

public class OffsiteSale : BaseEntity
{
    public required string DressName { get; set; }
    public required string Era { get; set; }
    public required string Category { get; set; }
    public required string Size { get; set; }
    public required string Condition { get; set; }
    public decimal SalePrice { get; set; }
    public decimal CostPrice { get; set; }
    public required string Platform { get; set; }
    public DateTime SaleDateUtc { get; set; }
    public string? Notes { get; set; }
}
