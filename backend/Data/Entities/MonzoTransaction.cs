namespace Eden_Relics_BE.Data.Entities;

public class MonzoTransaction : BaseEntity
{
    public string MonzoId { get; set; } = "";
    public DateTime Date { get; set; }
    public string Description { get; set; } = "";
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "GBP";
    public string Category { get; set; } = "";
    public string? MerchantName { get; set; }
    public string? MerchantLogo { get; set; }
    public string? Notes { get; set; }
    public string? Tags { get; set; }
    public bool IsLoad { get; set; }
    public string? DeclineReason { get; set; }
    public DateTime? SettledAt { get; set; }

    // User-assigned fields for bookkeeping
    public string? UserCategory { get; set; }
    public string? Platform { get; set; }
    public string? ReceiptUrl { get; set; }
}
