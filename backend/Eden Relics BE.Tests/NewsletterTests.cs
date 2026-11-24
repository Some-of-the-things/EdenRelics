using System.Net;
using System.Net.Http.Json;
using Eden_Relics_BE.Data;
using Eden_Relics_BE.Data.Entities;
using Eden_Relics_BE.DTOs;
using Eden_Relics_BE.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Eden_Relics_BE.Tests.Helpers;

namespace Eden_Relics_BE.Tests;

/// <summary>
/// Newsletters from the admin: compose, clean-up, preview, test send, sending in batches without
/// double-sending, scheduling, and one-click unsubscribe. Each test starts from an empty list.
/// </summary>
public class NewsletterTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public NewsletterTests(ApiFactory factory)
    {
        _factory = factory;
    }

    private FakeNewsletterMailer Mailer => _factory.Services.GetRequiredService<FakeNewsletterMailer>();

    private async Task ResetAsync()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        EdenRelicsDbContext db = scope.ServiceProvider.GetRequiredService<EdenRelicsDbContext>();
        db.NewsletterDeliveries.RemoveRange(db.NewsletterDeliveries.IgnoreQueryFilters());
        db.Newsletters.RemoveRange(db.Newsletters.IgnoreQueryFilters());
        db.MailingListSubscribers.RemoveRange(db.MailingListSubscribers.IgnoreQueryFilters());
        await db.SaveChangesAsync();
        // These tests count newsletter emails; keep the sign-up welcome code out of the outbox.
        await scope.ServiceProvider.GetRequiredService<ISignupOfferService>()
            .SaveSettingsAsync(SignupOfferService.Defaults with { Enabled = false });
        Mailer.Clear();
    }

    private async Task<HttpClient> AdminClient(string email)
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, email);
        return client;
    }

    private async Task SubscribeAsync(params string[] emails)
    {
        HttpClient anon = _factory.CreateClient();
        foreach (string e in emails)
        {
            (await anon.PostAsJsonAsync("/api/mailing-list/subscribe", new { email = e, source = "Homepage" })).EnsureSuccessStatusCode();
        }
    }

    private async Task<MailingListSubscriber> SubscriberAsync(string email)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        EdenRelicsDbContext db = scope.ServiceProvider.GetRequiredService<EdenRelicsDbContext>();
        return await db.MailingListSubscribers.AsNoTracking().FirstAsync(s => s.Email == email);
    }

    private async Task<int> ProcessAsync()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<INewsletterService>().ProcessQueueAsync();
    }

    private static async Task<NewsletterDto> CreateDraft(HttpClient admin, string subject = "New in: autumn florals", string body = "<p>Hello <strong>there</strong>.</p>")
    {
        HttpResponseMessage response = await admin.PostAsJsonAsync("/api/newsletters", new
        {
            subject,
            previewText = "Five new dresses this week",
            bodyHtml = body,
            editorMode = "visual",
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<NewsletterDto>(JsonOptions))!;
    }

    private static async Task<NewsletterDto> Get(HttpClient admin, Guid id)
    {
        return (await admin.GetFromJsonAsync<NewsletterDto>($"/api/newsletters/{id}", JsonOptions))!;
    }

    [Fact]
    public async Task Signup_RejectsJunkAddresses_AndGivesEachSubscriberAnUnsubscribeToken()
    {
        await ResetAsync();
        HttpClient anon = _factory.CreateClient();
        foreach (string junk in new[] { "not-an-email", "a@b", "someone@localhost", "two@@example.com", "x@example." })
        {
            HttpResponseMessage bad = await anon.PostAsJsonAsync("/api/mailing-list/subscribe", new { email = junk });
            Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        }

        await SubscribeAsync("  Real.Person@Example.com ");
        MailingListSubscriber sub = await SubscriberAsync("real.person@example.com");
        Assert.Equal("Homepage", sub.Source);
        Assert.False(string.IsNullOrEmpty(sub.UnsubscribeToken));
        Assert.True(sub.CreatedAtUtc > DateTime.UtcNow.AddMinutes(-5));
    }

    [Fact]
    public async Task Newsletters_AreAdminOnly()
    {
        HttpClient anon = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/newsletters")).StatusCode);

        HttpClient customer = _factory.CreateClient();
        await RegisterAndLogin(customer, "nl-customer@test.com");
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.GetAsync("/api/newsletters")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.GetAsync("/api/mailing-list/admin/subscribers")).StatusCode);
    }

    [Fact]
    public async Task PastedHtml_IsCleaned_SoItCantRunScripts()
    {
        await ResetAsync();
        HttpClient admin = await AdminClient("nl-clean@test.com");
        NewsletterDto draft = await CreateDraft(admin, body:
            "<html><head><style>p { color: #9B4A1E; }</style></head><body><p onclick=\"steal()\">Hi</p><script>alert(1)</script><a href=\"javascript:alert(1)\">x</a><img src=\"https://cdn.example.com/a.jpg\" alt=\"Dress\"></body></html>");

        Assert.DoesNotContain("<script", draft.BodyHtml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("onclick", draft.BodyHtml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("javascript:", draft.BodyHtml, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<style>", draft.BodyHtml);
        Assert.Contains("<img src=\"https://cdn.example.com/a.jpg\"", draft.BodyHtml);
    }

    [Fact]
    public async Task Preview_WrapsTheBodyInTheTemplate_WithFooterUnsubscribeAndInlinedCss()
    {
        HttpClient admin = await AdminClient("nl-preview@test.com");
        HttpResponseMessage response = await admin.PostAsJsonAsync("/api/newsletters/preview", new
        {
            subject = "Hello",
            previewText = "The grey line",
            bodyHtml = "<p>New pieces <a href=\"https://edenrelics.co.uk/shop\">in the shop</a>.</p>",
        });
        NewsletterPreviewDto preview = (await response.Content.ReadFromJsonAsync<NewsletterPreviewDto>(JsonOptions))!;

        Assert.Contains("Eden Relics Ltd", preview.Html);
        Assert.Contains("Unsubscribe", preview.Html);
        Assert.Contains("The grey line", preview.Html);
        Assert.Contains("EDEN RELICS", preview.Html);
        // The template's ".er-body p" rule ends up on the paragraph itself.
        Assert.Matches("<p style=\"[^\"]*margin", preview.Html);
        Assert.Contains("New pieces in the shop (https://edenrelics.co.uk/shop).", preview.Text);
        Assert.Contains("Unsubscribe:", preview.Text);
    }

    [Fact]
    public async Task TestSend_GoesToOneAddress_MarkedAsATest()
    {
        await ResetAsync();
        await SubscribeAsync("nl-test-sub@example.com");
        HttpClient admin = await AdminClient("nl-testsend@test.com");
        NewsletterDto draft = await CreateDraft(admin);

        HttpResponseMessage response = await admin.PostAsJsonAsync($"/api/newsletters/{draft.Id}/test", new { email = "teodora@example.com" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        OutgoingEmail sent = Assert.Single(Mailer.Sent);
        Assert.Equal("teodora@example.com", sent.To);
        Assert.Equal("[Test] New in: autumn florals", sent.Subject);
        Assert.Equal("Draft", (await Get(admin, draft.Id)).Status);
    }

    [Fact]
    public async Task Send_GoesToEveryActiveSubscriberOnce_WithTheirOwnUnsubscribeLinkAndHeaders()
    {
        await ResetAsync();
        await SubscribeAsync("a@example.com", "b@example.com", "c@example.com", "gone@example.com");
        HttpClient anon = _factory.CreateClient();
        await anon.PostAsJsonAsync("/api/mailing-list/unsubscribe", new { email = "gone@example.com" });

        HttpClient admin = await AdminClient("nl-send@test.com");
        Assert.Equal(3, (await admin.GetFromJsonAsync<NewsletterAudienceDto>("/api/newsletters/audience", JsonOptions))!.ActiveCount);

        NewsletterDto draft = await CreateDraft(admin);
        HttpResponseMessage send = await admin.PostAsJsonAsync($"/api/newsletters/{draft.Id}/send", new { scheduledForUtc = (DateTime?)null });
        NewsletterDto started = (await send.Content.ReadFromJsonAsync<NewsletterDto>(JsonOptions))!;
        Assert.Equal("Sending", started.Status);
        Assert.Equal(3, started.RecipientCount);

        await ProcessAsync();

        Assert.Equal(["a@example.com", "b@example.com", "c@example.com"], Mailer.Sent.Select(e => e.To).OrderBy(e => e));
        MailingListSubscriber a = await SubscriberAsync("a@example.com");
        OutgoingEmail toA = Mailer.Sent.Single(e => e.To == "a@example.com");
        Assert.Contains($"/unsubscribe?t={a.UnsubscribeToken}", toA.Html);
        Assert.Equal($"<https://api.edenrelics.co.uk/api/mailing-list/one-click/{a.UnsubscribeToken}>", toA.Headers!["List-Unsubscribe"]);
        Assert.Equal("List-Unsubscribe=One-Click", toA.Headers["List-Unsubscribe-Post"]);
        Assert.False(string.IsNullOrWhiteSpace(toA.Text));

        NewsletterDto done = await Get(admin, draft.Id);
        Assert.Equal("Sent", done.Status);
        Assert.Equal(3, done.SentCount);
        Assert.NotNull(done.SentAtUtc);

        // Running the queue again sends nothing more.
        Assert.Equal(0, await ProcessAsync());
        Assert.Equal(3, Mailer.Sent.Count);

        // A sent newsletter can't be edited or sent again.
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync($"/api/newsletters/{draft.Id}/send", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync($"/api/newsletters/{draft.Id}", new { subject = "x", bodyHtml = "<p>y</p>" })).StatusCode);
    }

    [Fact]
    public async Task LargeLists_GoInBatches_AndAFailedBatchIsRetriedUnderTheSameKey_SoNobodyGetsItTwice()
    {
        await ResetAsync();
        await SubscribeAsync(Enumerable.Range(1, 120).Select(i => $"reader{i:D3}@example.com").ToArray());
        HttpClient admin = await AdminClient("nl-batches@test.com");
        NewsletterDto draft = await CreateDraft(admin);

        Mailer.FailNextBatches = 1; // the first batch's call times out
        await admin.PostAsJsonAsync($"/api/newsletters/{draft.Id}/send", new { });

        await ProcessAsync();
        Assert.Empty(Mailer.Sent);
        NewsletterDto afterFailure = await Get(admin, draft.Id);
        Assert.Equal("Sending", afterFailure.Status);
        Assert.Equal("provider timed out", afterFailure.LastError);

        await ProcessAsync();

        Assert.Equal(120, Mailer.Sent.Count);
        Assert.Equal(120, Mailer.Sent.Select(e => e.To).Distinct().Count());
        // Batch 0 was tried twice under one key, then batches 1 and 2.
        Assert.Equal(4, Mailer.BatchKeys.Count);
        Assert.Equal(Mailer.BatchKeys[0], Mailer.BatchKeys[1]);
        Assert.Equal(3, Mailer.BatchKeys.Distinct().Count());

        NewsletterDto done = await Get(admin, draft.Id);
        Assert.Equal("Sent", done.Status);
        Assert.Equal(120, done.SentCount);
        Assert.Equal(0, done.FailedCount);
    }

    [Fact]
    public async Task UnsubscribingDuringASend_SkipsThemForTheBatchesNotYetSent()
    {
        await ResetAsync();
        await SubscribeAsync(Enumerable.Range(1, 60).Select(i => $"mid{i:D2}@example.com").ToArray());
        HttpClient admin = await AdminClient("nl-midsend@test.com");
        NewsletterDto draft = await CreateDraft(admin);
        await admin.PostAsJsonAsync($"/api/newsletters/{draft.Id}/send", new { });

        // Somebody leaves after the send has started, before their batch goes.
        MailingListSubscriber late = await SubscriberAsync("mid60@example.com");
        HttpResponseMessage oneClick = await _factory.CreateClient().PostAsync($"/api/mailing-list/one-click/{late.UnsubscribeToken}", new FormUrlEncodedContent(new Dictionary<string, string> { ["List-Unsubscribe"] = "One-Click" }));
        Assert.Equal(HttpStatusCode.OK, oneClick.StatusCode);

        await ProcessAsync();

        Assert.DoesNotContain(Mailer.Sent, e => e.To == "mid60@example.com");
        NewsletterDto done = await Get(admin, draft.Id);
        Assert.Equal(59, done.SentCount);
        Assert.Equal(1, done.SkippedCount);
    }

    [Fact]
    public async Task UnsubscribeLink_TakesEffectImmediately_AndCanBeUndone()
    {
        await ResetAsync();
        await SubscribeAsync("leaver@example.com");
        MailingListSubscriber sub = await SubscriberAsync("leaver@example.com");
        HttpClient anon = _factory.CreateClient();

        HttpResponseMessage response = await anon.PostAsJsonAsync("/api/mailing-list/unsubscribe-token", new { token = sub.UnsubscribeToken });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        MailingListSubscriber after = await SubscriberAsync("leaver@example.com");
        Assert.True(after.Unsubscribed);
        Assert.NotNull(after.UnsubscribedAtUtc);

        HttpClient admin = await AdminClient("nl-unsub@test.com");
        Assert.Equal(0, (await admin.GetFromJsonAsync<NewsletterAudienceDto>("/api/newsletters/audience", JsonOptions))!.ActiveCount);

        Assert.Equal(HttpStatusCode.NotFound, (await anon.PostAsJsonAsync("/api/mailing-list/unsubscribe-token", new { token = "0000000000000000000000000000" })).StatusCode);

        (await anon.PostAsJsonAsync("/api/mailing-list/resubscribe-token", new { token = sub.UnsubscribeToken })).EnsureSuccessStatusCode();
        Assert.False((await SubscriberAsync("leaver@example.com")).Unsubscribed);
    }

    [Fact]
    public async Task ScheduledSend_WaitsForItsTime_AndCanBeCancelled()
    {
        await ResetAsync();
        await SubscribeAsync("sched@example.com");
        HttpClient admin = await AdminClient("nl-schedule@test.com");
        NewsletterDto draft = await CreateDraft(admin);

        HttpResponseMessage response = await admin.PostAsJsonAsync($"/api/newsletters/{draft.Id}/send", new { scheduledForUtc = DateTime.UtcNow.AddDays(2) });
        Assert.Equal("Scheduled", (await response.Content.ReadFromJsonAsync<NewsletterDto>(JsonOptions))!.Status);
        await ProcessAsync();
        Assert.Empty(Mailer.Sent);

        Assert.Equal("Draft", (await (await admin.PostAsync($"/api/newsletters/{draft.Id}/cancel", null)).Content.ReadFromJsonAsync<NewsletterDto>(JsonOptions))!.Status);

        await admin.PostAsJsonAsync($"/api/newsletters/{draft.Id}/send", new { scheduledForUtc = DateTime.UtcNow.AddDays(1) });
        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            EdenRelicsDbContext db = scope.ServiceProvider.GetRequiredService<EdenRelicsDbContext>();
            Newsletter n = await db.Newsletters.FirstAsync(x => x.Id == draft.Id);
            n.ScheduledForUtc = DateTime.UtcNow.AddMinutes(-1); // its time has come
            await db.SaveChangesAsync();
        }
        await ProcessAsync();

        Assert.Equal("sched@example.com", Assert.Single(Mailer.Sent).To);
        Assert.Equal("Sent", (await Get(admin, draft.Id)).Status);
    }

    [Fact]
    public async Task EmptyNewsletters_AndEmptyLists_CantBeSent()
    {
        await ResetAsync();
        HttpClient admin = await AdminClient("nl-empty@test.com");
        NewsletterDto noSubject = await CreateDraft(admin, subject: "");
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync($"/api/newsletters/{noSubject.Id}/send", new { })).StatusCode);

        NewsletterDto noOne = await CreateDraft(admin);
        HttpResponseMessage response = await admin.PostAsJsonAsync($"/api/newsletters/{noOne.Id}/send", new { });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("no active subscribers", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task SubscriberAdmin_SearchesExportsUnsubscribesAndRemoves()
    {
        await ResetAsync();
        await SubscribeAsync("findme@example.com", "other@example.com", "=cmd@example.com");
        HttpClient admin = await AdminClient("nl-subs@test.com");

        List<AdminSubscriberDto> found = (await admin.GetFromJsonAsync<List<AdminSubscriberDto>>("/api/mailing-list/admin/subscribers?search=findme", JsonOptions))!;
        AdminSubscriberDto findMe = Assert.Single(found);
        Assert.Equal("Homepage", findMe.Source);

        string csv = await admin.GetStringAsync("/api/mailing-list/admin/subscribers.csv");
        Assert.Contains("\"findme@example.com\"", csv);
        Assert.Contains("\"'=cmd@example.com\"", csv); // can't run as a spreadsheet formula

        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsync($"/api/mailing-list/admin/subscribers/{findMe.Id}/unsubscribe", null)).StatusCode);
        List<AdminSubscriberDto> unsubscribed = (await admin.GetFromJsonAsync<List<AdminSubscriberDto>>("/api/mailing-list/admin/subscribers?status=unsubscribed", JsonOptions))!;
        Assert.Contains(unsubscribed, s => s.Email == "findme@example.com");

        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/mailing-list/admin/subscribers/{findMe.Id}")).StatusCode);
        using IServiceScope scope = _factory.Services.CreateScope();
        EdenRelicsDbContext db = scope.ServiceProvider.GetRequiredService<EdenRelicsDbContext>();
        Assert.False(await db.MailingListSubscribers.IgnoreQueryFilters().AnyAsync(s => s.Email == "findme@example.com"));

        // They can sign up again afterwards.
        await SubscribeAsync("findme@example.com");
    }
}
