using System.Net.Http.Json;
using static Eden_Relics_BE.Tests.Helpers;

namespace Eden_Relics_BE.Tests;

/// <summary>
/// Marking a piece sold has to record the income, whichever control does it.
/// </summary>
/// <remarks>
/// The ledger write lived inline in ProductsController, so the marketplace "Mark sold on..."
/// route set Status to Sold and recorded nothing — the natural way to log a Vinted/Depop sale
/// never moved total income (reported 2026-08-19). Both routes now go through ISalesLedgerService.
/// </remarks>
public class MarkSoldLedgerTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public MarkSoldLedgerTests(ApiFactory factory)
    {
        _factory = factory;
    }

    private static async Task<List<TransactionResponse>> RowsFor(HttpClient client, Guid productId)
    {
        List<TransactionResponse>? all =
            await client.GetFromJsonAsync<List<TransactionResponse>>("/api/finance", JsonOptions);
        return [.. all!.Where(t => t.Reference == productId.ToString())];
    }

    private static async Task<Guid> CreateProduct(
        HttpClient client, string name, decimal price, decimal? salePrice = null)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/products", new
        {
            name,
            description = "Test piece",
            price,
            salePrice,
            era = "1970s",
            category = "Dresses",
            size = "M",
            condition = "good",
            imageUrl = "https://example.com/img.jpg",
            inStock = true,
        });
        response.EnsureSuccessStatusCode();
        ProductResponse? created = await response.Content.ReadFromJsonAsync<ProductResponse>(JsonOptions);
        return created!.Id;
    }

    [Fact]
    public async Task MarkingSoldOnAMarketplace_RecordsTheIncome()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "marksold-income@test.com");

        Guid id = await CreateProduct(client, "Prairie dress", 65.00m);

        (await client.PostAsJsonAsync($"/api/marketplace/mark-sold/{id}", new { soldOn = "Vinted" }))
            .EnsureSuccessStatusCode();

        TransactionResponse sale = Assert.Single(await RowsFor(client, id), t => t.Category == "Sales");
        Assert.Equal(65.00m, sale.Amount);
    }

    [Fact]
    public async Task ItRecordsThePriceThePieceWasActuallySellingAt()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "marksold-saleprice@test.com");

        // Discounted: the ledger must follow the sale price, not the list price.
        Guid id = await CreateProduct(client, "Reduced tea dress", 80.00m, salePrice: 52.00m);

        (await client.PostAsJsonAsync($"/api/marketplace/mark-sold/{id}", new { soldOn = "Depop" }))
            .EnsureSuccessStatusCode();

        TransactionResponse sale = Assert.Single(await RowsFor(client, id), t => t.Category == "Sales");
        Assert.Equal(52.00m, sale.Amount);
    }

    [Fact]
    public async Task ItRecordsWhereThePieceSold()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "marksold-platform@test.com");

        Guid id = await CreateProduct(client, "Platform dress", 40.00m);

        (await client.PostAsJsonAsync($"/api/marketplace/mark-sold/{id}", new { soldOn = "Vinted" }))
            .EnsureSuccessStatusCode();

        // This route knows the platform, unlike the status-dropdown edit — so the row lands under
        // the Finance tab's "external" source filter rather than Unspecified.
        TransactionResponse sale = Assert.Single(await RowsFor(client, id), t => t.Category == "Sales");
        Assert.Equal("Vinted", sale.Platform);
    }

    [Fact]
    public async Task MarkingSoldTwice_DoesNotCountTheIncomeTwice()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "marksold-twice@test.com");

        Guid id = await CreateProduct(client, "Twice dress", 30.00m);

        (await client.PostAsJsonAsync($"/api/marketplace/mark-sold/{id}", new { soldOn = "Vinted" }))
            .EnsureSuccessStatusCode();
        (await client.PostAsJsonAsync($"/api/marketplace/mark-sold/{id}", new { soldOn = "Depop" }))
            .EnsureSuccessStatusCode();

        Assert.Single(await RowsFor(client, id), t => t.Category == "Sales");
    }

    [Fact]
    public async Task MarkingSoldOnAMarketplaceAfterTheStatusDropdown_DoesNotDoubleCount()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "marksold-both-routes@test.com");

        Guid id = await CreateProduct(client, "Both routes dress", 45.00m);

        (await client.PutAsJsonAsync($"/api/products/{id}", new { status = "sold" })).EnsureSuccessStatusCode();
        (await client.PostAsJsonAsync($"/api/marketplace/mark-sold/{id}", new { soldOn = "Vinted" }))
            .EnsureSuccessStatusCode();

        Assert.Single(await RowsFor(client, id), t => t.Category == "Sales");
    }

    [Fact]
    public async Task TheStatusDropdownStillRecordsTheIncome()
    {
        // The route that already worked, kept honest now the logic moved out of the controller.
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "marksold-dropdown@test.com");

        Guid id = await CreateProduct(client, "Dropdown dress", 55.00m);

        (await client.PutAsJsonAsync($"/api/products/{id}", new { status = "sold" })).EnsureSuccessStatusCode();

        TransactionResponse sale = Assert.Single(await RowsFor(client, id), t => t.Category == "Sales");
        Assert.Equal(55.00m, sale.Amount);
        Assert.Null(sale.Platform);
    }

    [Fact]
    public async Task MarkingSoldOnAMarketplace_MovesTotalIncome()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "marksold-summary@test.com");

        Guid id = await CreateProduct(client, "Summary dress", 70.00m);

        FinanceSummaryResponse? before =
            await client.GetFromJsonAsync<FinanceSummaryResponse>("/api/finance/summary", JsonOptions);

        (await client.PostAsJsonAsync($"/api/marketplace/mark-sold/{id}", new { soldOn = "Vinted" }))
            .EnsureSuccessStatusCode();

        FinanceSummaryResponse? after =
            await client.GetFromJsonAsync<FinanceSummaryResponse>("/api/finance/summary", JsonOptions);

        Assert.Equal(before!.TotalIncome + 70.00m, after!.TotalIncome);
    }
}
