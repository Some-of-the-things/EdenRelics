namespace Eden_Relics_BE.Services;

public interface ISellerPayoutService
{
    /// <summary>Create Pending payout rows for an order's NON-house-seller items (idempotent per
    /// order+seller). House-seller items are skipped — the platform already holds those funds.
    /// Returns the number of rows created.
    /// NOTE: the call from the live checkout webhook is a deferred change; this can be exercised
    /// directly (tests) meanwhile.</summary>
    Task<int> CreatePayoutsForOrderAsync(Guid orderId);

    /// <summary>Release every due Pending payout (release time passed, order not refunded/cancelled,
    /// seller payment-ready) via a Stripe transfer. Idempotent. Returns the number released.</summary>
    Task<int> ReleaseDuePayoutsAsync();
}
