namespace Eden_Relics_BE.Data.Entities;

public class Transaction : BaseEntity
{
    public DateTime Date { get; set; }
    public string Description { get; set; } = "";
    public decimal Amount { get; set; }
    public string Category { get; set; } = "";
    public string? Platform { get; set; }
    public string? Reference { get; set; }
    public string? ReceiptUrl { get; set; }
    public string? Notes { get; set; }
}
