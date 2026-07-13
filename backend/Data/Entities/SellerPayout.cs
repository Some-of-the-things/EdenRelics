namespace Eden_Relics_BE.Data.Entities;

public enum SellerPayoutStatus
{
    Pending = 0,
    Released = 1,
    Cancelled = 2,
}

/// <summary>
/// A per-seller amount owed from a paid order, to be transferred to the seller's Stripe connected
/// account after the buyer's 14-day cancellation window closes (separate charges & transfers, held
/// payout). House-seller items NEVER produce a payout — the platform already holds those funds, so
/// the existing single-seller checkout is unaffected.
///
/// The rows are created by <c>SellerPayoutService.CreatePayoutsForOrderAsync</c> and released by
/// <c>SellerPayoutReleaseService</c>. NOTE: the call that creates them from the live checkout webhook
/// is a deliberately-deferred change (it edits the functional payment path) — until it is wired in,
/// this whole mechanism stays dormant (no rows are created, the release job is a no-op).
/// </summary>
public class SellerPayout : BaseEntity
{
    public Guid OrderId { get; set; }
    public Order? Order { get; set; }

    public Guid SellerId { get; set; }
    public Seller? Seller { get; set; }

    /// <summary>The seller's items subtotal for this order (before commission).</summary>
    public decimal GrossAmount { get; set; }

    /// <summary>Platform commission withheld (default 15% of gross).</summary>
    public decimal Commission { get; set; }

    /// <summary>What the seller receives = <see cref="GrossAmount"/> − <see cref="Commission"/>.</summary>
    public decimal NetAmount { get; set; }

    /// <summary>Earliest UTC the payout may be released (sale time + the 14-day cancellation window).</summary>
    public DateTime ReleaseAfterUtc { get; set; }

    public SellerPayoutStatus Status { get; set; } = SellerPayoutStatus.Pending;

    /// <summary>Stripe Transfer id, set once released.</summary>
    public string? StripeTransferId { get; set; }

    public DateTime? ReleasedAtUtc { get; set; }
}
