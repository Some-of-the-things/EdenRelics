using System.Net;
using System.Net.Http.Json;
using Eden_Relics_BE.Data;
using Eden_Relics_BE.Data.Entities;
using Eden_Relics_BE.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Eden_Relics_BE.Tests.Helpers;

namespace Eden_Relics_BE.Tests;

/// <summary>
/// Reviews from real buyers: the request email a set number of days after dispatch, the tokened
/// link that needs no login, publishing straight away (or holding for a check), and the admin's
/// reply and hide-with-reason.
/// </summary>
public class VerifiedReviewTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    private static readonly Guid MaxiDressId = Guid.Parse("a1b2c3d4-0001-0000-0000-000000000001");

    public VerifiedReviewTests(ApiFactory factory)
    {
        _factory = factory;
    }

    private FakeNewsletterMailer Mailer => _factory.Services.GetRequiredService<FakeNewsletterMailer>();

    /// <summary>A guest order for the maxi dress, optionally already dispatched and with a review token.</summary>
    private async Task<Order> SeedGuestOrder(string email, string status = "Paid", DateTime? dispatchedAt = null, string? token = null)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        EdenRelicsDbContext db = scope.ServiceProvider.GetRequiredService<EdenRelicsDbContext>();
        Order order = new()
        {
            GuestEmail = email,
            Status = status,
            Total = 195m,
            DispatchedAtUtc = dispatchedAt,
            ReviewToken = token,
            Items = [new OrderItem { ProductId = MaxiDressId, ProductName = "Bohemian Maxi Dress", UnitPrice = 195m, Quantity = 1 }],
        };
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        return order;
    }

    private async Task<int> SendDueAsync()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IReviewRequestService>().SendDueAsync();
    }

    private async Task<Order> ReloadOrder(Guid id)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        EdenRelicsDbContext db = scope.ServiceProvider.GetRequiredService<EdenRelicsDbContext>();
        return await db.Orders.AsNoTracking().FirstAsync(o => o.Id == id);
    }

    private static object Review(string? token, int rating = 5, string comment = "Beautiful dress, exactly as described and so well packed.", string? first = "Sarah", string? last = "Kent", string? style = null) => new
    {
        token,
        rating,
        comment,
        firstName = first,
        lastName = last,
        nameStyle = style,
        photoUrl = (string?)null,
    };

    private async Task<List<PublicReviewDto>> PublicReviews()
    {
        return (await _factory.CreateClient().GetFromJsonAsync<List<PublicReviewDto>>("/api/reviews/public?take=100", JsonOptions))!;
    }

    [Fact]
    public async Task MarkingShipped_StartsTheClock_AndTheRequestGoesOutAfterTheSetDays_Once()
    {
        HttpClient admin = _factory.CreateClient();
        await RegisterAdmin(admin, _factory, "rv-clock@test.com");
        Order order = await SeedGuestOrder("buyer-clock@example.com");

        (await admin.PutAsJsonAsync($"/api/orders/admin/{order.Id}/status", new { status = "Shipped" })).EnsureSuccessStatusCode();
        Order shipped = await ReloadOrder(order.Id);
        Assert.NotNull(shipped.DispatchedAtUtc);

        Mailer.Clear();
        await SendDueAsync();
        Assert.DoesNotContain(Mailer.Sent, e => e.To == "buyer-clock@example.com"); // not 7 days yet

        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            EdenRelicsDbContext db = scope.ServiceProvider.GetRequiredService<EdenRelicsDbContext>();
            Order o = await db.Orders.FirstAsync(x => x.Id == order.Id);
            o.DispatchedAtUtc = DateTime.UtcNow.AddDays(-8);
            await db.SaveChangesAsync();
        }

        await SendDueAsync();
        OutgoingEmail email = Assert.Single(Mailer.Sent, e => e.To == "buyer-clock@example.com");
        Order after = await ReloadOrder(order.Id);
        Assert.NotNull(after.ReviewRequestSentAtUtc);
        Assert.Contains($"/review/{order.Id}?t={after.ReviewToken}", email.Html);
        Assert.Contains("Bohemian Maxi Dress", email.Html);
        Assert.Contains("Eden Relics Ltd", email.Html);

        // Marking it delivered later doesn't restart anything, and nobody is emailed twice.
        (await admin.PutAsJsonAsync($"/api/orders/admin/{order.Id}/status", new { status = "Delivered" })).EnsureSuccessStatusCode();
        await SendDueAsync();
        Assert.Single(Mailer.Sent, e => e.To == "buyer-clock@example.com");
        Assert.Equal(after.DispatchedAtUtc, (await ReloadOrder(order.Id)).DispatchedAtUtc);
    }

    [Fact]
    public async Task OneGentleReminder_GoesOutIfThereIsStillNoReview_AndNeverASecond()
    {
        Order order = await SeedGuestOrder("buyer-remind@example.com", "Shipped", DateTime.UtcNow.AddDays(-20), "remind-token-123");
        Order reviewed = await SeedGuestOrder("buyer-reviewed@example.com", "Shipped", DateTime.UtcNow.AddDays(-20), "reviewed-token-123");
        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            EdenRelicsDbContext db = scope.ServiceProvider.GetRequiredService<EdenRelicsDbContext>();
            foreach (Order o in await db.Orders.Where(x => x.Id == order.Id || x.Id == reviewed.Id).ToListAsync())
            {
                o.ReviewRequestSentAtUtc = DateTime.UtcNow.AddDays(-8);
            }
            await db.SaveChangesAsync();
        }
        (await _factory.CreateClient().PostAsJsonAsync($"/api/reviews/order/{reviewed.Id}", Review("reviewed-token-123"))).EnsureSuccessStatusCode();

        Mailer.Clear();
        await SendDueAsync();
        OutgoingEmail reminder = Assert.Single(Mailer.Sent, e => e.To == "buyer-remind@example.com");
        Assert.Contains("only one", reminder.Html);
        Assert.Contains($"/review/{order.Id}?t=remind-token-123", reminder.Html);
        Assert.DoesNotContain(Mailer.Sent, e => e.To == "buyer-reviewed@example.com");

        await SendDueAsync();
        Assert.Single(Mailer.Sent, e => e.To == "buyer-remind@example.com");
    }

    [Fact]
    public async Task TheReminder_CanBeSwitchedOff()
    {
        HttpClient admin = _factory.CreateClient();
        await RegisterAdmin(admin, _factory, "rv-remind-off@test.com");
        (await admin.PutAsJsonAsync("/api/reviews/admin/settings", new { requestDelayDays = 7, reminderDays = 0 })).EnsureSuccessStatusCode();
        Order order = await SeedGuestOrder("buyer-noremind@example.com", "Shipped", DateTime.UtcNow.AddDays(-20), "noremind-token-123");
        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            EdenRelicsDbContext db = scope.ServiceProvider.GetRequiredService<EdenRelicsDbContext>();
            Order o = await db.Orders.FirstAsync(x => x.Id == order.Id);
            o.ReviewRequestSentAtUtc = DateTime.UtcNow.AddDays(-8);
            await db.SaveChangesAsync();
        }
        Mailer.Clear();
        await SendDueAsync();
        Assert.DoesNotContain(Mailer.Sent, e => e.To == "buyer-noremind@example.com");
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync("/api/reviews/admin/settings", new { requestDelayDays = 7, reminderDays = 31 })).StatusCode);
        (await admin.PutAsJsonAsync("/api/reviews/admin/settings", new { requestDelayDays = 7, reminderDays = 7 })).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task OrdersDispatchedBeforeThisFeature_AreNeverEmailed()
    {
        Order old = await SeedGuestOrder("buyer-old@example.com", status: "Delivered", dispatchedAt: null);
        Mailer.Clear();
        await SendDueAsync();
        Assert.DoesNotContain(Mailer.Sent, e => e.To == "buyer-old@example.com");
        Assert.Null((await ReloadOrder(old.Id)).ReviewRequestSentAtUtc);
    }

    [Fact]
    public async Task TheDelayIsChangeableInTheAdmin_WithinLimits()
    {
        HttpClient admin = _factory.CreateClient();
        await RegisterAdmin(admin, _factory, "rv-settings@test.com");
        Assert.Equal(7, (await admin.GetFromJsonAsync<ReviewSettingsDto>("/api/reviews/admin/settings", JsonOptions))!.RequestDelayDays);

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync("/api/reviews/admin/settings", new { requestDelayDays = 0 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync("/api/reviews/admin/settings", new { requestDelayDays = 61 })).StatusCode);

        Order order = await SeedGuestOrder("buyer-delay@example.com", status: "Shipped", dispatchedAt: DateTime.UtcNow.AddDays(-4));
        Mailer.Clear();
        await SendDueAsync();
        Assert.DoesNotContain(Mailer.Sent, e => e.To == "buyer-delay@example.com");

        (await admin.PutAsJsonAsync("/api/reviews/admin/settings", new { requestDelayDays = 3 })).EnsureSuccessStatusCode();
        await SendDueAsync();
        Assert.Single(Mailer.Sent, e => e.To == "buyer-delay@example.com");

        (await admin.PutAsJsonAsync("/api/reviews/admin/settings", new { requestDelayDays = 7 })).EnsureSuccessStatusCode();
        Assert.NotNull((await ReloadOrder(order.Id)).ReviewToken);
    }

    [Fact]
    public async Task TheLinkOpensTheFormWithoutLogin_ButOnlyWithTheRightToken()
    {
        Order order = await SeedGuestOrder("buyer-link@example.com", status: "Shipped", dispatchedAt: DateTime.UtcNow, token: "link-token-0123456789abcdef");
        HttpClient anon = _factory.CreateClient();

        Assert.Equal(HttpStatusCode.NotFound, (await anon.GetAsync($"/api/reviews/order/{order.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await anon.GetAsync($"/api/reviews/order/{order.Id}?t=wrong-token-0123456789abcdef")).StatusCode);

        ReviewInviteDto invite = (await anon.GetFromJsonAsync<ReviewInviteDto>($"/api/reviews/order/{order.Id}?t=link-token-0123456789abcdef", JsonOptions))!;
        ReviewInviteItemDto item = Assert.Single(invite.Items);
        Assert.Equal("Bohemian Maxi Dress", item.Name);
        Assert.False(invite.AlreadyReviewed);

        Assert.Equal(HttpStatusCode.NotFound, (await anon.PostAsJsonAsync($"/api/reviews/order/{order.Id}", Review("wrong-token-0123456789abcdef"))).StatusCode);
    }

    [Fact]
    public async Task AReviewPublishesStraightAway_AsAVerifiedPurchase_WithThePieceAttached()
    {
        Order order = await SeedGuestOrder("buyer-publish@example.com", status: "Delivered", dispatchedAt: DateTime.UtcNow.AddDays(-9), token: "publish-token-0123456789abcd");
        HttpClient anon = _factory.CreateClient();

        HttpResponseMessage response = await anon.PostAsJsonAsync($"/api/reviews/order/{order.Id}", Review("publish-token-0123456789abcd", rating: 2, comment: "Lovely fabric but the hem had come down."));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        MyReviewDto mine = (await response.Content.ReadFromJsonAsync<MyReviewDto>(JsonOptions))!;
        Assert.Equal("Approved", mine.Status); // a 2-star review goes live like any other

        PublicReviewDto shown = Assert.Single(await PublicReviews(), r => r.Id == mine.Id);
        Assert.Equal(2, shown.Rating);
        Assert.Equal("Sarah K.", shown.AuthorDisplayName);
        Assert.Equal(["Bohemian Maxi Dress"], shown.ProductNames);

        List<PublicReviewDto> forPiece = (await anon.GetFromJsonAsync<List<PublicReviewDto>>($"/api/reviews/public/product/{MaxiDressId}", JsonOptions))!;
        Assert.Contains(forPiece, r => r.Id == mine.Id);

        // One review per order.
        Assert.Equal(HttpStatusCode.Conflict, (await anon.PostAsJsonAsync($"/api/reviews/order/{order.Id}", Review("publish-token-0123456789abcd"))).StatusCode);
    }

    [Fact]
    public async Task AnOrderNotYetSent_CantBeReviewed()
    {
        Order order = await SeedGuestOrder("buyer-early@example.com", status: "Paid", token: "early-token-0123456789abcdef");
        HttpResponseMessage response = await _factory.CreateClient().PostAsJsonAsync($"/api/reviews/order/{order.Id}", Review("early-token-0123456789abcdef"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ReviewsWithLinksOrContactDetails_WaitForACheck_InsteadOfGoingLive()
    {
        Order order = await SeedGuestOrder("buyer-held@example.com", status: "Shipped", dispatchedAt: DateTime.UtcNow, token: "held-token-0123456789abcdefg");
        HttpResponseMessage response = await _factory.CreateClient().PostAsJsonAsync($"/api/reviews/order/{order.Id}",
            Review("held-token-0123456789abcdefg", comment: "Great, also check out www.cheap-dresses.example for more!"));
        MyReviewDto mine = (await response.Content.ReadFromJsonAsync<MyReviewDto>(JsonOptions))!;

        Assert.Equal("Pending", mine.Status);
        Assert.DoesNotContain(await PublicReviews(), r => r.Id == mine.Id);
    }

    [Fact]
    public async Task TheAdminCanReply_AndHideOnlyWithAReason_WhichIsKept()
    {
        Order order = await SeedGuestOrder("buyer-admin@example.com", status: "Shipped", dispatchedAt: DateTime.UtcNow, token: "admin-token-0123456789abcdef");
        MyReviewDto mine = (await (await _factory.CreateClient().PostAsJsonAsync($"/api/reviews/order/{order.Id}", Review("admin-token-0123456789abcdef", style: "first"))).Content.ReadFromJsonAsync<MyReviewDto>(JsonOptions))!;

        HttpClient admin = _factory.CreateClient();
        await RegisterAdmin(admin, _factory, "rv-admin@test.com");

        AdminReviewDto replied = (await (await admin.PostAsJsonAsync($"/api/reviews/admin/{mine.Id}/reply", new { reply = "Thank you Sarah, enjoy it!" })).Content.ReadFromJsonAsync<AdminReviewDto>(JsonOptions))!;
        Assert.Equal("Thank you Sarah, enjoy it!", replied.Reply);
        Assert.Equal("buyer-admin@example.com", replied.UserEmail);
        PublicReviewDto shown = Assert.Single(await PublicReviews(), r => r.Id == mine.Id);
        Assert.Equal("Thank you Sarah, enjoy it!", shown.Reply);
        Assert.Equal("Sarah", shown.AuthorDisplayName);

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync($"/api/reviews/admin/{mine.Id}/hide", new { reason = "didnt-like-it" })).StatusCode);

        AdminReviewDto hidden = (await (await admin.PostAsJsonAsync($"/api/reviews/admin/{mine.Id}/hide", new { reason = "personal-data", note = "Included her phone number" })).Content.ReadFromJsonAsync<AdminReviewDto>(JsonOptions))!;
        Assert.Equal("Rejected", hidden.Status);
        Assert.Equal("personal-data", hidden.HiddenReason);
        Assert.Equal("Included her phone number", hidden.ModerationNote);
        Assert.DoesNotContain(await PublicReviews(), r => r.Id == mine.Id);

        (await admin.PostAsync($"/api/reviews/admin/{mine.Id}/publish", null)).EnsureSuccessStatusCode();
        Assert.Contains(await PublicReviews(), r => r.Id == mine.Id);

        HttpClient customer = _factory.CreateClient();
        await RegisterAndLogin(customer, "rv-not-admin@test.com");
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.PostAsJsonAsync($"/api/reviews/admin/{mine.Id}/reply", new { reply = "x" })).StatusCode);
    }

    [Fact]
    public async Task ASignedInBuyer_CanReviewTheirOwnOrder_WithoutTheLink()
    {
        HttpClient buyer = _factory.CreateClient();
        var (_, auth) = await RegisterAndLogin(buyer, "rv-account@test.com");
        Guid orderId;
        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            EdenRelicsDbContext db = scope.ServiceProvider.GetRequiredService<EdenRelicsDbContext>();
            Order order = new()
            {
                UserId = auth.User.Id,
                Status = "Shipped",
                Total = 195m,
                DispatchedAtUtc = DateTime.UtcNow,
                Items = [new OrderItem { ProductId = MaxiDressId, ProductName = "Bohemian Maxi Dress", UnitPrice = 195m, Quantity = 1 }],
            };
            db.Orders.Add(order);
            await db.SaveChangesAsync();
            orderId = order.Id;
        }

        ReviewInviteDto invite = (await buyer.GetFromJsonAsync<ReviewInviteDto>($"/api/reviews/order/{orderId}", JsonOptions))!;
        Assert.Equal("Test", invite.FirstName);
        Assert.Contains((await buyer.GetFromJsonAsync<List<EligibleOrderDto>>("/api/reviews/eligible", JsonOptions))!, o => o.OrderId == orderId);

        HttpResponseMessage response = await buyer.PostAsJsonAsync($"/api/reviews/order/{orderId}", Review(null, first: null, last: null));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        PublicReviewDto shown = Assert.Single(await PublicReviews(), r => r.Id == (response.Content.ReadFromJsonAsync<MyReviewDto>(JsonOptions).Result!.Id));
        Assert.Equal("Test U.", shown.AuthorDisplayName);

        // Someone else's account can't use it.
        HttpClient other = _factory.CreateClient();
        await RegisterAndLogin(other, "rv-someone-else@test.com");
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/reviews/order/{orderId}")).StatusCode);
    }

    [Theory]
    [InlineData("Sarah", "Kent", null, "Sarah K.")]
    [InlineData("Sarah", "kent", "first-initial", "Sarah K.")]
    [InlineData("Sarah", "Kent", "first", "Sarah")]
    [InlineData("Sarah", "Kent", "anonymous", "Verified buyer")]
    [InlineData("Sarah", "", null, "Sarah")]
    [InlineData("", "", null, "Verified buyer")]
    public void DisplayName_DefaultsToFirstNameAndInitial(string first, string last, string? style, string expected)
    {
        Assert.Equal(expected, ReviewService.BuildDisplayName(first, last, style));
    }

    [Theory]
    [InlineData("Gorgeous 1970s dress, fits 8 - 10 perfectly, worth every penny of £45.", null)]
    [InlineData("Arrived in two days. 1970 - 1980 style. Love it!", null)]
    [InlineData("Terrible, it arrived torn and I want my money back.", null)]
    [InlineData("Visit https://spam.example now", "Contains a link")]
    [InlineData("Email me at someone@example.com", "Contains an email address")]
    [InlineData("Call me on 07700 900123 about it", "Contains a phone number")]
    public void TheFilter_HoldsSpamAndPersonalData_NeverNegativeOpinions(string comment, string? expected)
    {
        Assert.Equal(expected, ReviewService.HoldReasonFor(comment));
    }
}
