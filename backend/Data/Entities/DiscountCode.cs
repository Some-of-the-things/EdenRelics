namespace Eden_Relics_BE.Data.Entities;

public enum DiscountKind
{
    Percent = 0,
    Fixed = 1,
}

/// <summary>
/// A unique, single-use code emailed to a new newsletter subscriber. It is tied to their email
/// address and checked at checkout. The offer terms are copied onto the code when it is issued,
/// so changing (or switching off) the offer later never changes a code already sent.
/// </summary>
public class DiscountCode : BaseEntity, IHardDeletable
{
    public string Code { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Source { get; set; } = "Signup";
    public DiscountKind Kind { get; set; }
    public decimal Amount { get; set; }

    /// <summary>Items must come to at least this much (before delivery) for the code to work.</summary>
    public decimal? MinOrder { get; set; }

    /// <summary>Pieces already reduced don't count towards the discount or the minimum.</summary>
    public bool ExcludeReduced { get; set; }

    /// <summary>The code can't be used on a gift order.</summary>
    public bool ExcludeGiftOrders { get; set; }

    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? UsedAtUtc { get; set; }
    public Guid? OrderId { get; set; }
}
