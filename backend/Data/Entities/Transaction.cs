namespace Eden_Relics_BE.Data.Entities;

/// <summary>
/// The ledger categories the code writes and matches on.
///
/// These are strings in the database and free text on the admin form, so this is a convention
/// rather than a constraint — but the automatic paths must agree on the spelling, because
/// idempotency checks match on <see cref="Transaction.Category"/> as well as
/// <see cref="Transaction.Reference"/>.
///
/// They have to: one product carries two rows keyed on its id — the stock purchase written when
/// it is created, and the sale written when it is marked sold. A check on Reference alone finds
/// the wrong one.
/// </summary>
public static class TransactionCategories
{
    public const string Sales = "Sales";
    public const string Stock = "Stock";
}

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
