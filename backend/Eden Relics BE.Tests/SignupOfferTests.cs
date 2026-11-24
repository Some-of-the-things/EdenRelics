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
/// Task 4: the newsletter sign-up discount. With it on, a new subscriber is emailed a unique,
/// single-use code tied to their address; with it off, nothing is offered or sent.
/// </summary>
public class SignupOfferTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public SignupOfferTests(ApiFactory factory)
    {
        _factory = factory;
    }

    private FakeNewsletterMailer Mailer => _factory.Services.GetRequiredService<FakeNewsletterMailer>();

    private static readonly Guid MaxiDressId = Guid.Parse("a1b2c3d4-0001-0000-0000-000000000001");

    private async Task<HttpClient> AdminClient(string email)
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, email);
        return client;
    }

    private async Task SaveOffer(HttpClient admin, object settings)
    {
        HttpResponseMessage r = await admin.PutAsJsonAsync("/api/discounts/admin/settings", settings);
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
    }

    private static object Offer(bool enabled = true, string kind = "percent", decimal amount = 10, decimal? minOrder = null,
        int validDays = 30, bool excludeReduced = false, bool excludeGift = false, bool earlyLine = false, string? text = null) => new
    {
        enabled,
        kind,
        amount,
        minOrder,
        validDays,
        signupText = text,
        excludeReduced,
        excludeGiftOrders = excludeGift,
        showEarlyAccessLine = earlyLine,
    };

    private async Task Subscribe(string email)
    {
        HttpResponseMessage r = await _factory.CreateClient().PostAsJsonAsync("/api/mailing-list/subscribe", new { email, source = "Homepage" });
        r.EnsureSuccessStatusCode();
    }

    private async Task<List<DiscountCode>> CodesFor(string email)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        EdenRelicsDbContext db = scope.ServiceProvider.GetRequiredService<EdenRelicsDbContext>();
        return await db.DiscountCodes.AsNoTracking().Where(c => c.Email == email).ToListAsync();
    }

    [Fact]
    public async Task WithTheOfferOn_ANewSubscriberIsEmailedTheirOwnCode_OnlyOnce()
    {
        HttpClient admin = await AdminClient("offer-admin1@example.com");
        await SaveOffer(admin, Offer(amount: 10));
        Mailer.Clear();

        await Subscribe("new-joiner@example.com");

        DiscountCode code = Assert.Single(await CodesFor("new-joiner@example.com"));
        Assert.Matches("^EDEN-[A-Z2-9]{4}-[A-Z2-9]{4}$", code.Code);
        Assert.Equal(10, code.Amount);
        OutgoingEmail email = Assert.Single(Mailer.Sent, e => e.To == "new-joiner@example.com");
        Assert.Contains(code.Code, email.Html);
        Assert.Contains(code.Code, email.Text);
        Assert.Contains("List-Unsubscribe", email.Headers!.Keys);

        // Unsubscribing and joining again doesn't earn a second code.
        await _factory.CreateClient().PostAsJsonAsync("/api/mailing-list/unsubscribe", new { email = "new-joiner@example.com" });
        await Subscribe("new-joiner@example.com");
        Assert.Single(await CodesFor("new-joiner@example.com"));
    }

    [Fact]
    public async Task IfTheWelcomeEmailFails_TheCodeIsTakenBack_SoJoiningAgainSendsOne()
    {
        HttpClient admin = await AdminClient("offer-admin6@example.com");
        await SaveOffer(admin, Offer(amount: 10));
        Mailer.Clear();
        Mailer.FailNextSends = 1;

        await Subscribe("unlucky@example.com");
        Assert.Empty(await CodesFor("unlucky@example.com"));

        await _factory.CreateClient().PostAsJsonAsync("/api/mailing-list/unsubscribe", new { email = "unlucky@example.com" });
        await Subscribe("unlucky@example.com");
        DiscountCode code = Assert.Single(await CodesFor("unlucky@example.com"));
        Assert.Contains(Mailer.Sent, e => e.To == "unlucky@example.com" && e.Html.Contains(code.Code));
    }

    [Fact]
    public async Task WithTheOfferOff_NoCodeIsSent_AndTheSiteMakesNoOffer()
    {
        HttpClient admin = await AdminClient("offer-admin2@example.com");
        await SaveOffer(admin, Offer(enabled: false));
        Mailer.Clear();

        await Subscribe("no-offer@example.com");

        Assert.Empty(await CodesFor("no-offer@example.com"));
        Assert.DoesNotContain(Mailer.Sent, e => e.To == "no-offer@example.com");
        PublicSignupOfferDto offer = (await _factory.CreateClient().GetFromJsonAsync<PublicSignupOfferDto>("/api/discounts/signup-offer", JsonOptions))!;
        Assert.False(offer.DiscountOn);
        Assert.Null(offer.Headline);

        await SaveOffer(admin, Offer(enabled: true));
    }

    [Fact]
    public async Task TheSignupBoxWording_FollowsTheSettings()
    {
        HttpClient admin = await AdminClient("offer-admin3@example.com");
        HttpClient anon = _factory.CreateClient();

        await SaveOffer(admin, Offer(kind: "fixed", amount: 5, minOrder: 40, earlyLine: true));
        PublicSignupOfferDto offer = (await anon.GetFromJsonAsync<PublicSignupOfferDto>("/api/discounts/signup-offer", JsonOptions))!;
        Assert.True(offer.DiscountOn);
        Assert.Equal("Join and get £5 off your first order over £40", offer.Headline);
        Assert.Equal(SignupOfferService.EarlyAccessLine, offer.EarlyAccessLine);

        await SaveOffer(admin, Offer(text: "Sign up for 10% off"));
        offer = (await anon.GetFromJsonAsync<PublicSignupOfferDto>("/api/discounts/signup-offer", JsonOptions))!;
        Assert.Equal("Sign up for 10% off", offer.Headline);
        Assert.Null(offer.EarlyAccessLine);
    }

    [Theory]
    [InlineData("percent", 95, null, 30)]
    [InlineData("percent", 0, null, 30)]
    [InlineData("fixed", 10, null, 0)]
    [InlineData("bogus", 10, null, 30)]
    public async Task SillySettings_AreRefused(string kind, decimal amount, decimal? minOrder, int validDays)
    {
        HttpClient admin = await AdminClient($"offer-admin-{kind}-{amount}-{validDays}@example.com");
        HttpResponseMessage r = await admin.PutAsJsonAsync("/api/discounts/admin/settings", Offer(kind: kind, amount: amount, minOrder: minOrder, validDays: validDays));
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
    }

    [Fact]
    public async Task SettingsAndCodes_AreAdminOnly()
    {
        HttpClient anon = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/discounts/admin/settings")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/discounts/admin/codes")).StatusCode);
    }

    [Fact]
    public async Task TheBasketCheck_WorksOnlyForTheAddressTheCodeWasSentTo()
    {
        HttpClient admin = await AdminClient("offer-admin4@example.com");
        await SaveOffer(admin, Offer(amount: 10));
        await Subscribe("checker@example.com");
        string code = Assert.Single(await CodesFor("checker@example.com")).Code;
        HttpClient anon = _factory.CreateClient();

        DiscountCheckResult ok = await Check(anon, code.ToLowerInvariant(), "Checker@Example.com");
        Assert.True(ok.Valid);
        Assert.True(ok.Discount > 0);
        Assert.Equal(code, ok.Code);

        DiscountCheckResult wrong = await Check(anon, code, "someone-else@example.com");
        Assert.False(wrong.Valid);
        Assert.Contains("different email", wrong.Message);

        DiscountCheckResult unknown = await Check(anon, "EDEN-NOPE-NOPE", "checker@example.com");
        Assert.False(unknown.Valid);

        AdminDiscountCodesDto codes = (await admin.GetFromJsonAsync<AdminDiscountCodesDto>("/api/discounts/admin/codes", JsonOptions))!;
        Assert.Contains(codes.Codes, c => c.Code == code && c.Status == "unused");
        Assert.True(codes.Unused >= 1);
    }

    private static async Task<DiscountCheckResult> Check(HttpClient client, string code, string email)
    {
        HttpResponseMessage r = await client.PostAsJsonAsync("/api/discounts/check", new { code, email, productIds = new[] { MaxiDressId }, isGift = false });
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        return (await r.Content.ReadFromJsonAsync<DiscountCheckResult>(JsonOptions))!;
    }

    [Fact]
    public async Task AnOrderWithSomeoneElsesCode_IsRefusedBeforePayment()
    {
        HttpClient admin = await AdminClient("offer-admin5@example.com");
        await SaveOffer(admin, Offer(amount: 10));
        await Subscribe("owner-of-code@example.com");
        string code = Assert.Single(await CodesFor("owner-of-code@example.com")).Code;

        HttpResponseMessage r = await _factory.CreateClient().PostAsJsonAsync("/api/orders", new
        {
            items = new[] { new { productId = MaxiDressId, quantity = 1 } },
            guestEmail = "thief@example.com",
            shippingMethod = "standard",
            discountCode = code,
        });
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        Assert.Contains("different email", await r.Content.ReadAsStringAsync());
    }

    private static DiscountCode Code(DiscountKind kind = DiscountKind.Percent, decimal amount = 10, decimal? min = null,
        bool excludeReduced = false, bool excludeGift = false, DateTime? expires = null, DateTime? used = null) => new()
    {
        Code = "EDEN-TEST-CODE",
        Email = "buyer@example.com",
        Kind = kind,
        Amount = amount,
        MinOrder = min,
        ExcludeReduced = excludeReduced,
        ExcludeGiftOrders = excludeGift,
        ExpiresAtUtc = expires ?? DateTime.UtcNow.AddDays(10),
        UsedAtUtc = used,
    };

    private static readonly DateTime Now = DateTime.UtcNow;

    [Fact]
    public void PercentOff_IsWorkedOutOnTheItems()
    {
        DiscountEvaluation r = SignupOfferService.Evaluate(Code(amount: 15), "buyer@example.com", [new(85, false), new(40.5m, false)], false, Now);
        Assert.Null(r.Error);
        Assert.Equal(18.83m, r.Discount); // 15% of 125.50 = 18.825
    }

    [Fact]
    public void PoundsOff_NeedsTheMinimum_AndNeverExceedsTheItems()
    {
        DiscountCode fiveOverForty = Code(DiscountKind.Fixed, 5, min: 40);
        Assert.NotNull(SignupOfferService.Evaluate(fiveOverForty, "buyer@example.com", [new(39.99m, false)], false, Now).Error);
        Assert.Equal(5, SignupOfferService.Evaluate(fiveOverForty, "buyer@example.com", [new(40, false)], false, Now).Discount);
        Assert.Equal(3, SignupOfferService.Evaluate(Code(DiscountKind.Fixed, 10), "buyer@example.com", [new(3, false)], false, Now).Discount);
    }

    [Fact]
    public void ReducedPieces_CanBeLeftOut_OfTheDiscountAndTheMinimum()
    {
        DiscountCode code = Code(amount: 10, min: 50, excludeReduced: true);
        DiscountEvaluation r = SignupOfferService.Evaluate(code, "buyer@example.com", [new(60, false), new(30, true)], false, Now);
        Assert.Equal(6, r.Discount);
        Assert.NotNull(SignupOfferService.Evaluate(code, "buyer@example.com", [new(40, false), new(30, true)], false, Now).Error);
        Assert.NotNull(SignupOfferService.Evaluate(code, "buyer@example.com", [new(80, true)], false, Now).Error);
    }

    [Fact]
    public void UsedExpiredGiftAndWrongAddress_AreAllRefused()
    {
        List<DiscountLine> basket = [new(100, false)];
        Assert.Contains("already been used", SignupOfferService.Evaluate(Code(used: Now), "buyer@example.com", basket, false, Now).Error);
        Assert.Contains("expired", SignupOfferService.Evaluate(Code(expires: Now.AddMinutes(-1)), "buyer@example.com", basket, false, Now).Error);
        Assert.Contains("gift", SignupOfferService.Evaluate(Code(excludeGift: true), "buyer@example.com", basket, true, Now).Error);
        Assert.Null(SignupOfferService.Evaluate(Code(), "buyer@example.com", basket, true, Now).Error);
        Assert.Contains("different email", SignupOfferService.Evaluate(Code(), "other@example.com", basket, false, Now).Error);
    }
}
