using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Eden_Relics_BE.Data;
using Eden_Relics_BE.Data.Entities;
using Eden_Relics_BE.DTOs;
using Eden_Relics_BE.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using static Eden_Relics_BE.Tests.Helpers;

namespace Eden_Relics_BE.Tests;

/// <summary>
/// Task 5: early access. A piece marked early access is hidden from everyone but subscribers
/// (their emailed link, or signed in with a subscribed account) until its release time, then
/// goes public by itself.
/// </summary>
public class EarlyAccessTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public EarlyAccessTests(ApiFactory factory)
    {
        _factory = factory;
    }

    private FakeNewsletterMailer Mailer => _factory.Services.GetRequiredService<FakeNewsletterMailer>();

    private record ProductRow(Guid Id, string Slug, DateTime? EarlyAccessUntilUtc);

    private async Task<HttpClient> AdminClient(string email)
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, email);
        return client;
    }

    /// <summary>Lists a new piece through the admin API, in early access by default.</summary>
    private async Task<ProductRow> ListPiece(HttpClient admin, string name, bool early = true, DateTime? release = null)
    {
        HttpResponseMessage r = await admin.PostAsJsonAsync("/api/products", new
        {
            name,
            description = "A lovely piece.",
            price = 60m,
            costPrice = 10m,
            era = "1970s",
            category = "1970s",
            size = "10",
            condition = "Excellent",
            imageUrl = "https://img.example/x.webp",
            inStock = true,
            earlyAccess = early,
            earlyAccessUntilUtc = release,
        });
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        return (await r.Content.ReadFromJsonAsync<ProductRow>(JsonOptions))!;
    }

    private async Task<MailingListSubscriber> Subscribe(string email)
    {
        (await _factory.CreateClient().PostAsJsonAsync("/api/mailing-list/subscribe", new { email, source = "Homepage" })).EnsureSuccessStatusCode();
        using IServiceScope scope = _factory.Services.CreateScope();
        EdenRelicsDbContext db = scope.ServiceProvider.GetRequiredService<EdenRelicsDbContext>();
        return await db.MailingListSubscribers.AsNoTracking().FirstAsync(s => s.Email == email);
    }

    private string TokenFor(Guid subscriberId, DateTime? expires = null)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<IEarlyAccessService>()
            .CreateToken(subscriberId, expires ?? DateTime.UtcNow.AddDays(14));
    }

    private static async Task<List<Guid>> PublicIds(HttpClient client)
    {
        List<ProductRow> rows = (await client.GetFromJsonAsync<List<ProductRow>>("/api/products", JsonOptions))!;
        return rows.Select(p => p.Id).ToList();
    }

    [Fact]
    public async Task AnEarlyAccessPiece_IsHiddenFromThePublic_EverywhereItCouldBeFound()
    {
        HttpClient admin = await AdminClient("ea-admin1@example.com");
        ProductRow piece = await ListPiece(admin, "Early Paisley Maxi");
        Assert.NotNull(piece.EarlyAccessUntilUtc);
        // Default window: about 48 hours.
        Assert.InRange(piece.EarlyAccessUntilUtc!.Value, DateTime.UtcNow.AddHours(47), DateTime.UtcNow.AddHours(49));

        HttpClient anon = _factory.CreateClient();
        Assert.DoesNotContain(piece.Id, await PublicIds(anon));
        Assert.Equal(HttpStatusCode.NotFound, (await anon.GetAsync($"/api/products/by-slug/{piece.Slug}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await anon.GetAsync($"/api/products/{piece.Id}")).StatusCode);
        List<ProductRow> category = (await anon.GetFromJsonAsync<List<ProductRow>>("/api/products/category/1970s", JsonOptions))!;
        Assert.DoesNotContain(category, p => p.Id == piece.Id);
        string sitemap = await anon.GetStringAsync("/api/sitemap.xml");
        Assert.DoesNotContain(piece.Slug, sitemap);
        EarlyAccessListDto list = (await anon.GetFromJsonAsync<EarlyAccessListDto>("/api/products/early-access", JsonOptions))!;
        Assert.False(list.Access);
        Assert.Empty(list.Pieces);

        // The admin still sees it, with its release time.
        List<ProductRow> adminRows = (await admin.GetFromJsonAsync<List<ProductRow>>("/api/products", JsonOptions))!;
        Assert.NotNull(Assert.Single(adminRows, p => p.Id == piece.Id).EarlyAccessUntilUtc);
    }

    [Fact]
    public async Task ASubscribersLink_UnlocksIt_UntilTheyLeaveTheList()
    {
        HttpClient admin = await AdminClient("ea-admin2@example.com");
        ProductRow piece = await ListPiece(admin, "Early Velvet Blazer");
        MailingListSubscriber sub = await Subscribe("ea-fan@example.com");

        HttpClient withLink = _factory.CreateClient();
        withLink.DefaultRequestHeaders.Add(EarlyAccessService.HeaderName, TokenFor(sub.Id));
        Assert.Contains(piece.Id, await PublicIds(withLink));
        Assert.Equal(HttpStatusCode.OK, (await withLink.GetAsync($"/api/products/by-slug/{piece.Slug}")).StatusCode);
        EarlyAccessListDto list = (await withLink.GetFromJsonAsync<EarlyAccessListDto>("/api/products/early-access", JsonOptions))!;
        Assert.True(list.Access);
        Assert.Contains(list.Pieces, p => p.Id == piece.Id);

        // A tampered or expired token does nothing.
        HttpClient tampered = _factory.CreateClient();
        tampered.DefaultRequestHeaders.Add(EarlyAccessService.HeaderName, TokenFor(sub.Id)[..^2] + "xx");
        Assert.DoesNotContain(piece.Id, await PublicIds(tampered));
        HttpClient expired = _factory.CreateClient();
        expired.DefaultRequestHeaders.Add(EarlyAccessService.HeaderName, TokenFor(sub.Id, DateTime.UtcNow.AddMinutes(-1)));
        Assert.DoesNotContain(piece.Id, await PublicIds(expired));

        // Leaving the list ends access, even with the link.
        await _factory.CreateClient().PostAsJsonAsync("/api/mailing-list/unsubscribe", new { email = "ea-fan@example.com" });
        Assert.DoesNotContain(piece.Id, await PublicIds(withLink));
    }

    [Fact]
    public async Task OpeningTheLink_DoesNotSignAnyoneUp()
    {
        Guid stranger = Guid.NewGuid();
        HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(EarlyAccessService.HeaderName, TokenFor(stranger));
        EarlyAccessListDto list = (await client.GetFromJsonAsync<EarlyAccessListDto>("/api/products/early-access", JsonOptions))!;
        Assert.False(list.Access);

        using IServiceScope scope = _factory.Services.CreateScope();
        EdenRelicsDbContext db = scope.ServiceProvider.GetRequiredService<EdenRelicsDbContext>();
        Assert.False(await db.MailingListSubscribers.IgnoreQueryFilters().AnyAsync(s => s.Id == stranger));
    }

    [Fact]
    public async Task ASignedInSubscriber_SeesItWithoutTheLink()
    {
        HttpClient admin = await AdminClient("ea-admin3@example.com");
        ProductRow piece = await ListPiece(admin, "Early Silk Scarf Dress");

        HttpClient member = _factory.CreateClient();
        await RegisterAndLogin(member, "ea-member@example.com");
        Assert.DoesNotContain(piece.Id, await PublicIds(member));

        await Subscribe("ea-member@example.com");
        Assert.Contains(piece.Id, await PublicIds(member));
    }

    [Fact]
    public async Task AtTheReleaseTime_ItGoesPublicByItself()
    {
        HttpClient admin = await AdminClient("ea-admin4@example.com");
        ProductRow piece = await ListPiece(admin, "Early Corduroy Pinafore");
        HttpClient anon = _factory.CreateClient();
        Assert.DoesNotContain(piece.Id, await PublicIds(anon));

        // Wind the clock on: the release time has passed.
        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            EdenRelicsDbContext db = scope.ServiceProvider.GetRequiredService<EdenRelicsDbContext>();
            Product p = await db.Products.FirstAsync(x => x.Id == piece.Id);
            p.EarlyAccessUntilUtc = DateTime.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
        }

        Assert.Contains(piece.Id, await PublicIds(anon));
        Assert.Equal(HttpStatusCode.OK, (await anon.GetAsync($"/api/products/by-slug/{piece.Slug}")).StatusCode);
        Assert.Contains(piece.Slug, await anon.GetStringAsync("/api/sitemap.xml"));
    }

    [Fact]
    public async Task AReleaseTimeInThePast_IsRefused_AndEarlyAccessCanBeTurnedOff()
    {
        HttpClient admin = await AdminClient("ea-admin5@example.com");
        HttpResponseMessage bad = await admin.PostAsJsonAsync("/api/products", new
        {
            name = "Past release",
            description = "x",
            price = 10m,
            costPrice = 1m,
            era = "1970s",
            category = "1970s",
            size = "10",
            condition = "Good",
            imageUrl = "https://img.example/x.webp",
            inStock = true,
            earlyAccess = true,
            earlyAccessUntilUtc = DateTime.UtcNow.AddHours(-1),
        });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);

        ProductRow piece = await ListPiece(admin, "Early then public");
        (await admin.PutAsJsonAsync($"/api/products/{piece.Id}", new { earlyAccess = false })).EnsureSuccessStatusCode();
        Assert.Contains(piece.Id, await PublicIds(_factory.CreateClient()));
    }

    [Fact]
    public async Task OnlySubscribers_CanBuyIt_WhileItsInEarlyAccess()
    {
        HttpClient admin = await AdminClient("ea-admin6@example.com");
        ProductRow piece = await ListPiece(admin, "Early Lace Blouse");

        HttpResponseMessage r = await _factory.CreateClient().PostAsJsonAsync("/api/orders", new
        {
            items = new[] { new { productId = piece.Id, quantity = 1 } },
            guestEmail = "not-a-subscriber@example.com",
            shippingMethod = "standard",
        });
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        Assert.Contains("not available", await r.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task TheDropEmail_GivesEachSubscriberTheirOwnWorkingLink()
    {
        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            EdenRelicsDbContext db = scope.ServiceProvider.GetRequiredService<EdenRelicsDbContext>();
            db.MailingListSubscribers.RemoveRange(db.MailingListSubscribers.IgnoreQueryFilters());
            await db.SaveChangesAsync();
        }
        HttpClient admin = await AdminClient("ea-admin7@example.com");
        MailingListSubscriber a = await Subscribe("drop-a@example.com");
        MailingListSubscriber b = await Subscribe("drop-b@example.com");
        Mailer.Clear();

        HttpResponseMessage created = await admin.PostAsJsonAsync("/api/newsletters", new
        {
            subject = "Early access: new pieces",
            previewText = "Before anyone else",
            bodyHtml = $"<p><a href=\"https://edenrelics.co.uk/early-access?ea={NewsletterService.EarlyAccessPlaceholder}\">See them first</a></p>",
            editorMode = "visual",
        });
        NewsletterDto n = (await created.Content.ReadFromJsonAsync<NewsletterDto>(JsonOptions))!;
        Assert.Contains(NewsletterService.EarlyAccessPlaceholder, n.BodyHtml);
        (await admin.PostAsJsonAsync($"/api/newsletters/{n.Id}/send", new { scheduledForUtc = (DateTime?)null })).EnsureSuccessStatusCode();
        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<INewsletterService>().ProcessQueueAsync();
        }

        OutgoingEmail toA = Assert.Single(Mailer.Sent, e => e.To == "drop-a@example.com");
        OutgoingEmail toB = Assert.Single(Mailer.Sent, e => e.To == "drop-b@example.com");
        Assert.DoesNotContain(NewsletterService.EarlyAccessPlaceholder, toA.Html);
        Assert.DoesNotContain(NewsletterService.EarlyAccessPlaceholder, toA.Text);
        string tokenA = System.Text.RegularExpressions.Regex.Match(toA.Html, @"ea=([A-Za-z0-9._-]+)").Groups[1].Value;
        string tokenB = System.Text.RegularExpressions.Regex.Match(toB.Html, @"ea=([A-Za-z0-9._-]+)").Groups[1].Value;
        Assert.NotEqual(tokenA, tokenB);

        using IServiceScope check = _factory.Services.CreateScope();
        EarlyAccessService ea = (EarlyAccessService)check.ServiceProvider.GetRequiredService<IEarlyAccessService>();
        Assert.Equal(a.Id, ea.Validate(tokenA, DateTime.UtcNow));
        Assert.Equal(b.Id, ea.Validate(tokenB, DateTime.UtcNow));
    }

    // --- Open/click/bounce figures from Resend's webhook ---

    private string Sign(string id, string timestamp, string body)
    {
        string secret = _factory.Services.GetRequiredService<IConfiguration>()["Resend:WebhookSecret"]!;
        byte[] key = Convert.FromBase64String(secret["whsec_".Length..]);
        return "v1," + Convert.ToBase64String(HMACSHA256.HashData(key, Encoding.UTF8.GetBytes($"{id}.{timestamp}.{body}")));
    }

    private async Task<HttpStatusCode> PostWebhook(string body, bool goodSignature = true)
    {
        string id = "msg_" + Guid.NewGuid().ToString("N");
        string ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        HttpRequestMessage request = new(HttpMethod.Post, "/api/webhooks/resend")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("svix-id", id);
        request.Headers.Add("svix-timestamp", ts);
        request.Headers.Add("svix-signature", goodSignature ? Sign(id, ts, body) : "v1,AAAA");
        return (await _factory.CreateClient().SendAsync(request)).StatusCode;
    }

    [Fact]
    public async Task Opens_Clicks_AndBounces_AreCounted_AndABounceLeavesTheList()
    {
        MailingListSubscriber sub = await Subscribe("tracked@example.com");
        Guid newsletterId;
        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            EdenRelicsDbContext db = scope.ServiceProvider.GetRequiredService<EdenRelicsDbContext>();
            Newsletter n = new() { Subject = "Tracked", Status = NewsletterStatus.Sent, SentAtUtc = DateTime.UtcNow };
            db.Newsletters.Add(n);
            db.NewsletterDeliveries.Add(new NewsletterDelivery
            {
                NewsletterId = n.Id,
                SubscriberId = sub.Id,
                Email = sub.Email,
                Status = NewsletterDeliveryStatus.Sent,
                ProviderId = "email-123",
            });
            await db.SaveChangesAsync();
            newsletterId = n.Id;
        }

        Assert.Equal(HttpStatusCode.Unauthorized, await PostWebhook("""{"type":"email.opened","data":{"email_id":"email-123"}}""", goodSignature: false));
        Assert.Equal(HttpStatusCode.OK, await PostWebhook("""{"type":"email.clicked","data":{"email_id":"email-123"}}"""));
        Assert.Equal(HttpStatusCode.OK, await PostWebhook("""{"type":"email.bounced","data":{"email_id":"email-123"}}"""));
        Assert.Equal(HttpStatusCode.OK, await PostWebhook("""{"type":"email.opened","data":{"email_id":"not-ours"}}"""));

        HttpClient admin = await AdminClient("ea-admin8@example.com");
        List<NewsletterDto> list = (await admin.GetFromJsonAsync<List<NewsletterDto>>("/api/newsletters", JsonOptions))!;
        NewsletterDto row = Assert.Single(list, x => x.Id == newsletterId);
        Assert.Equal(1, row.OpenedCount);
        Assert.Equal(1, row.ClickedCount);
        Assert.Equal(1, row.BouncedCount);

        using IServiceScope after = _factory.Services.CreateScope();
        EdenRelicsDbContext db2 = after.ServiceProvider.GetRequiredService<EdenRelicsDbContext>();
        Assert.True((await db2.MailingListSubscribers.AsNoTracking().FirstAsync(s => s.Id == sub.Id)).Unsubscribed);
    }
}
