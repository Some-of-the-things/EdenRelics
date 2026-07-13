using Eden_Relics_BE.Data;
using Eden_Relics_BE.Data.Entities;
using Eden_Relics_BE.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Eden_Relics_BE.Tests;

public class SellerPayoutTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public SellerPayoutTests(ApiFactory factory)
    {
        _factory = factory;
    }

    private static async Task<Guid> SeedSellerAsync(EdenRelicsDbContext db, bool onboarded = true, decimal? rate = null)
    {
        Seller s = new()
        {
            BusinessName = "Payout Vintage",
            Slug = "payout-" + Guid.NewGuid().ToString("N")[..8],
            ApprovalStatus = SellerApprovalStatus.Approved,
            IsHouse = false,
            StripeConnectedAccountId = onboarded ? "acct_test_" + Guid.NewGuid().ToString("N")[..8] : null,
            ConnectOnboardingComplete = onboarded,
            CommissionRate = rate,
        };
        db.Sellers.Add(s);
        await db.SaveChangesAsync();
        return s.Id;
    }

    private static async Task<Guid> SeedOrderAsync(EdenRelicsDbContext db, Guid sellerId, decimal unitPrice, int qty = 1, string status = "Paid")
    {
        Order o = new()
        {
            Status = status,
            Total = unitPrice * qty,
            Items = { new OrderItem { ProductId = Guid.NewGuid(), SellerId = sellerId, ProductName = "Test piece", UnitPrice = unitPrice, Quantity = qty } },
        };
        db.Orders.Add(o);
        await db.SaveChangesAsync();
        return o.Id;
    }

    [Fact]
    public async Task CreatePayouts_ComputesCommissionAndNet()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        EdenRelicsDbContext db = scope.ServiceProvider.GetRequiredService<EdenRelicsDbContext>();
        ISellerPayoutService svc = scope.ServiceProvider.GetRequiredService<ISellerPayoutService>();

        Guid sellerId = await SeedSellerAsync(db);
        Guid orderId = await SeedOrderAsync(db, sellerId, 100m);

        int created = await svc.CreatePayoutsForOrderAsync(orderId);

        Assert.Equal(1, created);
        SellerPayout payout = await db.SellerPayouts.FirstAsync(p => p.OrderId == orderId);
        Assert.Equal(100m, payout.GrossAmount);
        Assert.Equal(15m, payout.Commission);      // default 15%
        Assert.Equal(85m, payout.NetAmount);
        Assert.Equal(SellerPayoutStatus.Pending, payout.Status);
        Assert.True(payout.ReleaseAfterUtc > DateTime.UtcNow.AddDays(13));
    }

    [Fact]
    public async Task CreatePayouts_ExcludesHouseSellerItems()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        EdenRelicsDbContext db = scope.ServiceProvider.GetRequiredService<EdenRelicsDbContext>();
        ISellerPayoutService svc = scope.ServiceProvider.GetRequiredService<ISellerPayoutService>();

        Guid orderId = await SeedOrderAsync(db, HouseSeller.Id, 100m);

        int created = await svc.CreatePayoutsForOrderAsync(orderId);

        Assert.Equal(0, created);
        Assert.False(await db.SellerPayouts.AnyAsync(p => p.OrderId == orderId));
    }

    [Fact]
    public async Task CreatePayouts_IsIdempotent()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        EdenRelicsDbContext db = scope.ServiceProvider.GetRequiredService<EdenRelicsDbContext>();
        ISellerPayoutService svc = scope.ServiceProvider.GetRequiredService<ISellerPayoutService>();

        Guid sellerId = await SeedSellerAsync(db);
        Guid orderId = await SeedOrderAsync(db, sellerId, 50m);

        await svc.CreatePayoutsForOrderAsync(orderId);
        int second = await svc.CreatePayoutsForOrderAsync(orderId);

        Assert.Equal(0, second);
        Assert.Equal(1, await db.SellerPayouts.CountAsync(p => p.OrderId == orderId));
    }

    [Fact]
    public async Task CreatePayouts_RespectsSellerCommissionOverride()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        EdenRelicsDbContext db = scope.ServiceProvider.GetRequiredService<EdenRelicsDbContext>();
        ISellerPayoutService svc = scope.ServiceProvider.GetRequiredService<ISellerPayoutService>();

        Guid sellerId = await SeedSellerAsync(db, rate: 0.25m);
        Guid orderId = await SeedOrderAsync(db, sellerId, 200m);

        await svc.CreatePayoutsForOrderAsync(orderId);

        SellerPayout payout = await db.SellerPayouts.FirstAsync(p => p.OrderId == orderId);
        Assert.Equal(50m, payout.Commission);   // 25% of 200
        Assert.Equal(150m, payout.NetAmount);
    }

    [Fact]
    public async Task ReleaseDue_ReleasesPastDue_NotFuture()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        EdenRelicsDbContext db = scope.ServiceProvider.GetRequiredService<EdenRelicsDbContext>();
        ISellerPayoutService svc = scope.ServiceProvider.GetRequiredService<ISellerPayoutService>();

        Guid sellerId = await SeedSellerAsync(db);
        Guid dueOrder = await SeedOrderAsync(db, sellerId, 100m);
        Guid futureOrder = await SeedOrderAsync(db, sellerId, 100m);

        SellerPayout duePayout = new()
        {
            OrderId = dueOrder, SellerId = sellerId, GrossAmount = 100m, Commission = 15m, NetAmount = 85m,
            ReleaseAfterUtc = DateTime.UtcNow.AddDays(-1), Status = SellerPayoutStatus.Pending,
        };
        SellerPayout futurePayout = new()
        {
            OrderId = futureOrder, SellerId = sellerId, GrossAmount = 100m, Commission = 15m, NetAmount = 85m,
            ReleaseAfterUtc = DateTime.UtcNow.AddDays(5), Status = SellerPayoutStatus.Pending,
        };
        db.SellerPayouts.AddRange(duePayout, futurePayout);
        await db.SaveChangesAsync();

        await svc.ReleaseDuePayoutsAsync();

        SellerPayout releasedDue = await db.SellerPayouts.AsNoTracking().FirstAsync(p => p.Id == duePayout.Id);
        SellerPayout stillFuture = await db.SellerPayouts.AsNoTracking().FirstAsync(p => p.Id == futurePayout.Id);
        Assert.Equal(SellerPayoutStatus.Released, releasedDue.Status);
        Assert.False(string.IsNullOrWhiteSpace(releasedDue.StripeTransferId));
        Assert.Equal(SellerPayoutStatus.Pending, stillFuture.Status);
    }

    [Fact]
    public async Task ReleaseDue_SkipsRefundedOrder()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        EdenRelicsDbContext db = scope.ServiceProvider.GetRequiredService<EdenRelicsDbContext>();
        ISellerPayoutService svc = scope.ServiceProvider.GetRequiredService<ISellerPayoutService>();

        Guid sellerId = await SeedSellerAsync(db);
        Guid refundedOrder = await SeedOrderAsync(db, sellerId, 100m, status: "Refunded");

        SellerPayout payout = new()
        {
            OrderId = refundedOrder, SellerId = sellerId, GrossAmount = 100m, Commission = 15m, NetAmount = 85m,
            ReleaseAfterUtc = DateTime.UtcNow.AddDays(-1), Status = SellerPayoutStatus.Pending,
        };
        db.SellerPayouts.Add(payout);
        await db.SaveChangesAsync();

        await svc.ReleaseDuePayoutsAsync();

        SellerPayout after = await db.SellerPayouts.AsNoTracking().FirstAsync(p => p.Id == payout.Id);
        Assert.Equal(SellerPayoutStatus.Pending, after.Status);   // never release on a refunded order
    }
}
