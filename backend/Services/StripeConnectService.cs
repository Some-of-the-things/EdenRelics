using Stripe;

namespace Eden_Relics_BE.Services;

/// <summary>Real Stripe Connect (Express) implementation. The secret key is read per-call from
/// configuration, mirroring OrderService's existing pattern.</summary>
public class StripeConnectService(IConfiguration configuration) : IStripeConnectService
{
    private void SetKey() => StripeConfiguration.ApiKey = configuration["Stripe:SecretKey"];

    public async Task<string> CreateAccountAsync(string? email)
    {
        SetKey();
        Account account = await new AccountService().CreateAsync(new AccountCreateOptions
        {
            Type = "express",
            Email = string.IsNullOrWhiteSpace(email) ? null : email,
            Capabilities = new AccountCapabilitiesOptions
            {
                CardPayments = new AccountCapabilitiesCardPaymentsOptions { Requested = true },
                Transfers = new AccountCapabilitiesTransfersOptions { Requested = true },
            },
        });
        return account.Id;
    }

    public async Task<string> CreateAccountLinkAsync(string connectedAccountId, string returnUrl, string refreshUrl)
    {
        SetKey();
        AccountLink link = await new AccountLinkService().CreateAsync(new AccountLinkCreateOptions
        {
            Account = connectedAccountId,
            ReturnUrl = returnUrl,
            RefreshUrl = refreshUrl,
            Type = "account_onboarding",
        });
        return link.Url;
    }

    public async Task<(bool ChargesEnabled, bool PayoutsEnabled)> GetAccountStatusAsync(string connectedAccountId)
    {
        SetKey();
        Account account = await new AccountService().GetAsync(connectedAccountId);
        return (account.ChargesEnabled, account.PayoutsEnabled);
    }

    public async Task<string> CreateTransferAsync(string connectedAccountId, long amountMinor, string currency, string idempotencyKey)
    {
        SetKey();
        Transfer transfer = await new TransferService().CreateAsync(
            new TransferCreateOptions
            {
                Amount = amountMinor,
                Currency = currency,
                Destination = connectedAccountId,
            },
            new RequestOptions { IdempotencyKey = idempotencyKey });
        return transfer.Id;
    }
}
