using Eden_Relics_BE.Data.Entities;

namespace Eden_Relics_BE.Services;

/// <summary>
/// Wraps the Stripe Connect (Express) SDK calls behind an interface so the seller-onboarding
/// orchestration (create account → onboarding link → status refresh) is testable with a fake and
/// the real Stripe calls are isolated in one place. Used only for seller payouts; the platform's
/// own (house-seller) checkout is unaffected.
/// </summary>
public interface IStripeConnectService
{
    /// <summary>Create a new Express connected account (requesting card_payments + transfers).
    /// Returns the "acct_…" id.</summary>
    Task<string> CreateAccountAsync(string? email);

    /// <summary>Create an account-onboarding Account Link the seller follows to complete KYC/payouts.
    /// Returns the hosted onboarding URL.</summary>
    Task<string> CreateAccountLinkAsync(string connectedAccountId, string returnUrl, string refreshUrl);

    /// <summary>Whether the connected account can currently accept charges and receive payouts.</summary>
    Task<(bool ChargesEnabled, bool PayoutsEnabled)> GetAccountStatusAsync(string connectedAccountId);
}
