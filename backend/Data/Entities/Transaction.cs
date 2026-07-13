namespace Eden_Relics_BE.Data.Entities;

public class Transaction : BaseEntity
{
    public DateTime Date { get; set; }
    public string Description { get; set; } = "";
    public decimal Amount { get; set; }
    public string Category { get; set; } = "";
    public string? Platform { get; set; }

    /// <summary>The seller this ledger row is attributed to (seller sales, per-seller COGS/payouts).
    /// Null for platform-level rows (commission income, platform expenses).</summary>
    public Guid? SellerId { get; set; }
    public string? Reference { get; set; }
    public string? ReceiptUrl { get; set; }
    public string? Notes { get; set; }
}
