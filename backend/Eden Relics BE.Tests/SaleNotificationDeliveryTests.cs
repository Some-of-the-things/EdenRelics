using System.Net;
using System.Net.Http.Json;
using Eden_Relics_BE.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using static Eden_Relics_BE.Tests.Helpers;

namespace Eden_Relics_BE.Tests;

/// <summary>
/// Covers the bug these tests exist for: the sale notification used to be fire-and-forget over
/// the REQUEST's DbContext, so the favourites query ran after the response completed, threw
/// ObjectDisposedException into a swallowed catch, and no email was ever sent. Asserting a 200
/// (as the older tests did) could never have caught that. These assert the email itself.
///
/// The test host strips every IHostedService, so the queue is drained explicitly via
/// SaleNotificationBackgroundService.DrainAsync — the same code path the hosted service runs.
/// </summary>
[Collection(SaleNotificationCollection.Name)]
public class SaleNotificationDeliveryTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public SaleNotificationDeliveryTests(ApiFactory factory)
    {
        _factory = factory;
    }

    private static readonly ILogger Logger = NullLogger.Instance;

    /// <summary>
    /// Drains the queue through this fixture's DI scope.
    ///
    /// The returned count is deliberately not asserted on: the queue is process-wide, and test
    /// classes outside this collection (anything that PUTs a salePrice) enqueue into it too, so
    /// the count is not this test's to predict. Their product ids don't exist in this fixture's
    /// in-memory database, so they produce no emails — which is why the OUTBOX is the sound
    /// thing to assert on.
    /// </summary>
    private async Task<int> DrainNotifications()
    {
        IServiceScopeFactory scopeFactory = _factory.Services.GetRequiredService<IServiceScopeFactory>();
        return await SaleNotificationBackgroundService.DrainAsync(scopeFactory, Logger);
    }

    /// <summary>
    /// The notification queue and the fake's outbox are both process-wide statics, so anything
    /// another test left behind has to be cleared before this one can assert on counts.
    /// </summary>
    private async Task ResetNotifications()
    {
        await DrainNotifications();
        FakeEmailService.ClearSentSaleNotifications();
    }

    /// <summary>
    /// Favourites with the sale-alert box ticked. Posting no body opts OUT
    /// (<c>NotifyOnSale = dto?.NotifyOnSale ?? false</c>), so a test that skips this asserts
    /// nothing about delivery.
    /// </summary>
    private static async Task FavouriteWithSaleAlerts(HttpClient client, Guid productId)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/favourites/{productId}", new { notifyOnSale = true });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private static async Task<Guid> CreateProduct(HttpClient client, string name, decimal price)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/products", new
        {
            name,
            description = "Desc",
            price,
            era = "1990s",
            category = "90s",
            size = "10",
            condition = "good",
            imageUrl = "https://example.com/img.jpg",
            inStock = true
        });
        response.EnsureSuccessStatusCode();
        ProductResponse? created = await response.Content.ReadFromJsonAsync<ProductResponse>(JsonOptions);
        return created!.Id;
    }

    [Fact]
    public async Task SettingSalePrice_ActuallyEmailsTheFavouriter()
    {
        await ResetNotifications();

        HttpClient adminClient = _factory.CreateClient();
        await RegisterAdmin(adminClient, _factory, "admin-notify-delivery@test.com");
        Guid productId = await CreateProduct(adminClient, "Delivered Sale Dress", 120m);

        HttpClient userClient = _factory.CreateClient();
        await RegisterAndLogin(userClient, "user-notify-delivery@test.com");
        await FavouriteWithSaleAlerts(userClient, productId);

        HttpResponseMessage update = await adminClient.PutAsJsonAsync(
            $"/api/products/{productId}", new { salePrice = 90m });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        await DrainNotifications();

        List<FakeEmailService.SentSaleNotification> sent = FakeEmailService.SentSaleNotifications();
        FakeEmailService.SentSaleNotification email = Assert.Single(sent);
        Assert.Equal("user-notify-delivery@test.com", email.ToEmail);
        Assert.Equal("Delivered Sale Dress", email.ProductName);
        Assert.Equal(120m, email.OriginalPrice);
        Assert.Equal(90m, email.SalePrice);
    }

    [Fact]
    public async Task BulkSale_WithNotifyFavourites_EmailsEachFavouritedPiece()
    {
        await ResetNotifications();

        HttpClient adminClient = _factory.CreateClient();
        await RegisterAdmin(adminClient, _factory, "admin-bulk-delivery@test.com");
        Guid first = await CreateProduct(adminClient, "Bulk Delivered A", 100m);
        Guid second = await CreateProduct(adminClient, "Bulk Delivered B", 200m);

        HttpClient userClient = _factory.CreateClient();
        await RegisterAndLogin(userClient, "user-bulk-delivery@test.com");
        await FavouriteWithSaleAlerts(userClient, first);
        await FavouriteWithSaleAlerts(userClient, second);

        HttpResponseMessage response = await adminClient.PostAsJsonAsync("/api/products/bulk-sale", new
        {
            productIds = new[] { first, second },
            discountPercent = 25m,
            notifyFavourites = true
        });
        response.EnsureSuccessStatusCode();

        await DrainNotifications();

        List<FakeEmailService.SentSaleNotification> sent = FakeEmailService.SentSaleNotifications();
        Assert.Equal(2, sent.Count);
        Assert.Equal(75m, sent.Single(s => s.ProductName == "Bulk Delivered A").SalePrice);
        Assert.Equal(150m, sent.Single(s => s.ProductName == "Bulk Delivered B").SalePrice);
    }

    [Fact]
    public async Task BulkSale_WithoutNotifyFavourites_SendsNothing()
    {
        await ResetNotifications();

        HttpClient adminClient = _factory.CreateClient();
        await RegisterAdmin(adminClient, _factory, "admin-bulk-silent@test.com");
        Guid productId = await CreateProduct(adminClient, "Bulk Silent Dress", 100m);

        HttpClient userClient = _factory.CreateClient();
        await RegisterAndLogin(userClient, "user-bulk-silent@test.com");
        await FavouriteWithSaleAlerts(userClient, productId);

        HttpResponseMessage response = await adminClient.PostAsJsonAsync("/api/products/bulk-sale", new
        {
            productIds = new[] { productId },
            discountPercent = 20m
        });
        response.EnsureSuccessStatusCode();

        await DrainNotifications();
        Assert.Empty(FakeEmailService.SentSaleNotifications());
    }

    [Fact]
    public async Task FavouriterWhoDidNotAskForAlerts_IsNotEmailed()
    {
        await ResetNotifications();

        HttpClient adminClient = _factory.CreateClient();
        await RegisterAdmin(adminClient, _factory, "admin-notify-optout@test.com");
        Guid productId = await CreateProduct(adminClient, "Opted Out Dress", 100m);

        HttpClient userClient = _factory.CreateClient();
        await RegisterAndLogin(userClient, "user-notify-optout@test.com");
        // No body: the sale-alert box was left unticked.
        HttpResponseMessage fav = await userClient.PostAsync($"/api/favourites/{productId}", null);
        Assert.Equal(HttpStatusCode.Created, fav.StatusCode);

        HttpResponseMessage update = await adminClient.PutAsJsonAsync(
            $"/api/products/{productId}", new { salePrice = 70m });
        update.EnsureSuccessStatusCode();

        await DrainNotifications();
        Assert.Empty(FakeEmailService.SentSaleNotifications());
    }

    [Fact]
    public async Task SaleClearedBeforeTheQueueDrains_SendsNothing()
    {
        await ResetNotifications();

        HttpClient adminClient = _factory.CreateClient();
        await RegisterAdmin(adminClient, _factory, "admin-notify-raced@test.com");
        Guid productId = await CreateProduct(adminClient, "Raced Sale Dress", 100m);

        HttpClient userClient = _factory.CreateClient();
        await RegisterAndLogin(userClient, "user-notify-raced@test.com");
        await FavouriteWithSaleAlerts(userClient, productId);

        HttpResponseMessage applied = await adminClient.PutAsJsonAsync(
            $"/api/products/{productId}", new { salePrice = 80m });
        applied.EnsureSuccessStatusCode();

        // Admin changes their mind before the queue is processed. Announcing a reduction on a
        // piece that is back at full price would be worse than sending nothing.
        HttpResponseMessage cleared = await adminClient.PostAsJsonAsync("/api/products/bulk-sale/clear", new
        {
            productIds = new[] { productId }
        });
        cleared.EnsureSuccessStatusCode();

        await DrainNotifications();
        Assert.Empty(FakeEmailService.SentSaleNotifications());
    }
}
