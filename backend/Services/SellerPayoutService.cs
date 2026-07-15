using Eden_Relics_BE.Data.Entities;
using Eden_Relics_BE.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Eden_Relics_BE.Services;

public class SellerPayoutService(
    IRepository<SellerPayout> payouts,
    IRepository<Order> orders,
    IRepository<Seller> sellers,
    IStripeConnectService connect,
    IOptions<MarketplaceOptions> options) : ISellerPayoutService
{
    public async Task<int> CreatePayoutsForOrderAsync(Guid orderId)
    {
        Order? order = await orders.Query().Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == orderId);
        if (order is null)
        {
            return 0;
        }

        DateTime releaseAfter = DateTime.UtcNow.AddDays(14);
        int created = 0;

        foreach (IGrouping<Guid, OrderItem> group in order.Items
                     .Where(i => i.SellerId != HouseSeller.Id)   // house items never produce a payout
                     .GroupBy(i => i.SellerId))
        {
            Guid sellerId = group.Key;
            // Idempotent per (order, seller).
            if (await payouts.Query().AnyAsync(p => p.OrderId == orderId && p.SellerId == sellerId))
            {
                continue;
            }

            decimal gross = group.Sum(i => i.UnitPrice * i.Quantity);
            Seller? seller = await sellers.GetByIdAsync(sellerId);
            decimal rate = seller?.CommissionRate ?? options.Value.DefaultCommissionRate;
            decimal commission = Math.Round(gross * rate, 2, MidpointRounding.AwayFromZero);

            await payouts.AddAsync(new SellerPayout
            {
                OrderId = orderId,
                SellerId = sellerId,
                GrossAmount = gross,
                Commission = commission,
                NetAmount = gross - commission,
                ReleaseAfterUtc = releaseAfter,
                Status = SellerPayoutStatus.Pending,
            });
            created++;
        }

        return created;
    }

    public async Task<int> ReleaseDuePayoutsAsync()
    {
        DateTime now = DateTime.UtcNow;
        List<SellerPayout> due = await payouts.Query()
            .Where(p => p.Status == SellerPayoutStatus.Pending && p.ReleaseAfterUtc <= now)
            .ToListAsync();

        int released = 0;
        foreach (SellerPayout payout in due)
        {
            Order? order = await orders.GetByIdAsync(payout.OrderId);
            if (order is null || order.Status is "Refunded" or "Cancelled")
            {
                continue;   // never release funds for a reversed order
            }

            Seller? seller = await sellers.GetByIdAsync(payout.SellerId);
            if (seller?.StripeConnectedAccountId is null || !seller.ConnectOnboardingComplete)
            {
                continue;   // seller not payment-ready
            }

            long amountMinor = (long)Math.Round(payout.NetAmount * 100m, MidpointRounding.AwayFromZero);
            if (amountMinor <= 0)
            {
                continue;
            }

            // Idempotency key keyed on the payout id makes a retry after a crash safe.
            string transferId = await connect.CreateTransferAsync(
                seller.StripeConnectedAccountId, amountMinor, "gbp", $"payout_{payout.Id}");

            payout.Status = SellerPayoutStatus.Released;
            payout.StripeTransferId = transferId;
            payout.ReleasedAtUtc = now;
            await payouts.UpdateAsync(payout);
            released++;
        }

        return released;
    }
}
