using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Eden_Relics_BE.Data;
using Eden_Relics_BE.Data.Entities;
using Eden_Relics_BE.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using static Eden_Relics_BE.Tests.Helpers;

namespace Eden_Relics_BE.Tests;

/// <summary>
/// The "This is a gift" checkout option: what gets stored, what Teodora's notification and the
/// buyer's email show, and that an unticked order is unchanged.
/// </summary>
public class GiftOrderTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    private static readonly Guid SeededProductId = Guid.Parse("a1b2c3d4-0001-0000-0000-000000000001");

    public GiftOrderTests(ApiFactory factory)
    {
        _factory = factory;
    }

    // ---- NormaliseGift ----

    [Fact]
    public void NormaliseGift_Unticked_DropsBothFields()
    {
        OrderService.GiftDetails gift = OrderService.NormaliseGift(false, "Happy birthday", "Ann");
        Assert.Null(gift.Message);
        Assert.Null(gift.To);
        Assert.Null(gift.Error);
    }

    [Fact]
    public void NormaliseGift_Ticked_TrimsAndBlanksBecomeNull()
    {
        OrderService.GiftDetails gift = OrderService.NormaliseGift(true, "  Love you  ", "   ");
        Assert.Equal("Love you", gift.Message);
        Assert.Null(gift.To);
        Assert.Null(gift.Error);
    }

    [Fact]
    public void NormaliseGift_TickedWithNoText_IsAllowed()
    {
        OrderService.GiftDetails gift = OrderService.NormaliseGift(true, null, null);
        Assert.Null(gift.Error);
    }

    [Fact]
    public void NormaliseGift_MessageAtLimit_Passes_OverLimit_Fails()
    {
        Assert.Null(OrderService.NormaliseGift(true, new string('a', 200), null).Error);
        Assert.NotNull(OrderService.NormaliseGift(true, new string('a', 201), null).Error);
    }

    [Fact]
    public void NormaliseGift_ToAtLimit_Passes_OverLimit_Fails()
    {
        Assert.Null(OrderService.NormaliseGift(true, null, new string('b', 40)).Error);
        Assert.NotNull(OrderService.NormaliseGift(true, null, new string('b', 41)).Error);
    }

    [Fact]
    public void NormaliseGift_KeepsMessageLineBreaks_StripsControlChars()
    {
        OrderService.GiftDetails gift = OrderService.NormaliseGift(true, "Dear Ann,\r\nEnjoy!\u0007", "An\nn");
        Assert.Equal("Dear Ann,\nEnjoy!", gift.Message);
        Assert.Equal("An n", gift.To);
    }

    [Fact]
    public void NormaliseGift_KeepsHtmlCharactersAsTyped()
    {
        // Escaping happens at render time, so the stored text is exactly what the buyer typed.
        OrderService.GiftDetails gift = OrderService.NormaliseGift(true, "<b>\"Hi\" & 'bye'</b>", "<Ann>");
        Assert.Equal("<b>\"Hi\" & 'bye'</b>", gift.Message);
        Assert.Equal("<Ann>", gift.To);
    }

    // ---- API validation (runs before Stripe is called) ----

    [Fact]
    public async Task Create_GiftMessageOver200_Returns400()
    {
        HttpClient client = _factory.CreateClient();
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/orders", new
        {
            items = new[] { new { productId = SeededProductId, quantity = 1 } },
            guestEmail = "gift-too-long@test.com",
            isGift = true,
            giftMessage = new string('x', 201),
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_GiftToOver40_Returns400()
    {
        HttpClient client = _factory.CreateClient();
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/orders", new
        {
            items = new[] { new { productId = SeededProductId, quantity = 1 } },
            guestEmail = "gift-name-too-long@test.com",
            isGift = true,
            giftTo = new string('x', 41),
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---- Admin view ----

    [Fact]
    public async Task AdminOrders_ShowGiftFields()
    {
        HttpClient client = _factory.CreateClient();
        var (_, auth) = await RegisterAdmin(client, _factory, "gift-admin@test.com");

        Guid orderId;
        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            EdenRelicsDbContext db = scope.ServiceProvider.GetRequiredService<EdenRelicsDbContext>();
            Order order = NewOrder(isGift: true, message: "Happy Christmas <3", to: "Ann");
            order.UserId = db.Users.First(u => u.Email == auth.User.Email).Id;
            db.Orders.Add(order);
            await db.SaveChangesAsync();
            orderId = order.Id;
        }

        string json = await client.GetStringAsync("/api/orders/admin/all");
        JsonElement row = JsonDocument.Parse(json).RootElement.EnumerateArray()
            .First(o => o.GetProperty("id").GetGuid() == orderId);
        Assert.True(row.GetProperty("isGift").GetBoolean());
        Assert.Equal("Happy Christmas <3", row.GetProperty("giftMessage").GetString());
        Assert.Equal("Ann", row.GetProperty("giftTo").GetString());
    }

    // ---- Teodora's notification ----

    [Fact]
    public void OwnerNotification_Gift_HasGiftSubjectAndDetailsAboveTheItems()
    {
        Order order = NewOrder(isGift: true, message: "Dear Ann,\nEnjoy it!", to: "Ann");
        (string subject, string html) = EmailService.BuildOwnerSaleNotification(order, "https://example.test");

        Assert.StartsWith("GIFT", subject);
        int giftAt = html.IndexOf("GIFT ORDER", StringComparison.Ordinal);
        int itemsAt = html.IndexOf("Bohemian Maxi Dress", StringComparison.Ordinal);
        Assert.True(giftAt >= 0 && giftAt < itemsAt, "Gift details should come before the items.");
        Assert.Contains("Dear Ann,<br/>Enjoy it!", html);
        Assert.Contains(">Ann<", html);
    }

    [Fact]
    public void OwnerNotification_Gift_EscapesBuyerText()
    {
        Order order = NewOrder(isGift: true, message: "<script>alert(1)</script> \"quoted\" & co", to: "<img src=x>");
        (_, string html) = EmailService.BuildOwnerSaleNotification(order, "https://example.test");

        Assert.DoesNotContain("<script>", html);
        Assert.DoesNotContain("<img src=x>", html);
        Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt; &quot;quoted&quot; &amp; co", html);
        Assert.Contains("&lt;img src=x&gt;", html);
    }

    [Fact]
    public void OwnerNotification_NormalOrder_IsUnchanged()
    {
        Order order = NewOrder(isGift: false, message: null, to: null);
        (string subject, string html) = EmailService.BuildOwnerSaleNotification(order, "https://example.test");

        Assert.Equal("New sale — £195.00 — Eden Relics", subject);
        Assert.DoesNotContain("GIFT", html);
    }

    // ---- Buyer's confirmation email ----

    private const string GiftLine =
        "Your order is marked as a gift. We'll leave the prices out and include a card about the piece, with your message on the back.";

    [Fact]
    public void BuyerEmail_Gift_IncludesGiftLine()
    {
        string html = BuildEmailService().BuildOrderInvoiceHtml(NewOrder(isGift: true, message: "Hi", to: "Ann"));
        Assert.Contains(GiftLine, html);
    }

    [Fact]
    public void BuyerEmail_NormalOrder_HasNoGiftLine()
    {
        string html = BuildEmailService().BuildOrderInvoiceHtml(NewOrder(isGift: false, message: null, to: null));
        Assert.DoesNotContain("marked as a gift", html);
    }

    [Fact]
    public void BuyerEmail_ChristmasOrder_ShowsExtendedReturns()
    {
        Order order = NewOrder(isGift: false, message: null, to: null);
        order.CreatedAtUtc = new DateTime(2026, 11, 20, 12, 0, 0, DateTimeKind.Utc);
        string html = BuildEmailService().BuildOrderInvoiceHtml(order);
        Assert.Contains("until 23 January 2027", html);
    }

    [Fact]
    public void BuyerEmail_AfterChristmasWindow_ShowsStandardReturns()
    {
        Order order = NewOrder(isGift: false, message: null, to: null);
        order.CreatedAtUtc = new DateTime(2026, 12, 25, 12, 0, 0, DateTimeKind.Utc);
        string html = BuildEmailService().BuildOrderInvoiceHtml(order);
        Assert.DoesNotContain("23 January 2027", html);
        Assert.Contains("Returns accepted within 14 days.", html);
    }

    [Fact]
    public void BuyerEmail_MarketplaceSend_ShowsStandardReturns()
    {
        Order order = NewOrder(isGift: false, message: null, to: null);
        order.CreatedAtUtc = new DateTime(2026, 11, 20, 12, 0, 0, DateTimeKind.Utc);
        string html = BuildEmailService().BuildOrderInvoiceHtml(order, "Etsy");
        Assert.DoesNotContain("23 January 2027", html);
    }

    [Theory]
    [InlineData(2026, 10, 31, 23, false)]
    [InlineData(2026, 11, 1, 0, true)]
    [InlineData(2026, 12, 24, 23, true)]
    [InlineData(2026, 12, 25, 0, false)]
    public void ChristmasReturns_WindowEdges(int y, int m, int d, int h, bool covered)
    {
        Assert.Equal(covered, ChristmasReturns.Covers(new DateTime(y, m, d, h, 30, 0, DateTimeKind.Utc)));
    }

    private static EmailService BuildEmailService()
    {
        IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection().Build();
        return new EmailService(null!, config, NullLogger<EmailService>.Instance);
    }

    private static Order NewOrder(bool isGift, string? message, string? to) => new()
    {
        Status = "Paid",
        Total = 195m,
        GuestEmail = "buyer@test.com",
        ShippingMethod = "standard",
        ShippingCost = 0m,
        IsGift = isGift,
        GiftMessage = message,
        GiftTo = to,
        Items =
        [
            new OrderItem
            {
                ProductId = SeededProductId,
                ProductName = "Bohemian Maxi Dress",
                UnitPrice = 195m,
                Quantity = 1
            }
        ]
    };
}
