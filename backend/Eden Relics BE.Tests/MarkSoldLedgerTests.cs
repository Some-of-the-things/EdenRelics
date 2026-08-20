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
    public async Task AddingASalePriceAfterTheSaleWasLogged_CorrectsTheLedger()
    {
        // Teodora's case (2026-08-19): marked sold (or backfilled) before the sale price was set,
        // so the ledger took the list price and then never moved when the sale price was added.
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "marksold-late-saleprice@test.com");

        Guid id = await CreateProduct(client, "Forgot the discount", 80.00m);

        (await client.PutAsJsonAsync($"/api/products/{id}", new { status = "sold" })).EnsureSuccessStatusCode();
        Assert.Equal(80.00m, Assert.Single(await RowsFor(client, id), t => t.Category == "Sales").Amount);

        (await client.PutAsJsonAsync($"/api/products/{id}", new { salePrice = 52.00m })).EnsureSuccessStatusCode();

        TransactionResponse sale = Assert.Single(await RowsFor(client, id), t => t.Category == "Sales");
        Assert.Equal(52.00m, sale.Amount);
    }

    [Fact]
    public async Task CorrectingTheSalePriceOnASoldPiece_MovesTheLedgerWithIt()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "marksold-correct-saleprice@test.com");

        Guid id = await CreateProduct(client, "Corrected dress", 90.00m, salePrice: 60.00m);
        (await client.PutAsJsonAsync($"/api/products/{id}", new { status = "sold" })).EnsureSuccessStatusCode();

        (await client.PutAsJsonAsync($"/api/products/{id}", new { salePrice = 65.00m })).EnsureSuccessStatusCode();

        Assert.Equal(65.00m, Assert.Single(await RowsFor(client, id), t => t.Category == "Sales").Amount);
    }

    [Fact]
    public async Task ClearingTheSalePriceOnASoldPiece_FallsBackToTheListPrice()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "marksold-clear-saleprice@test.com");

        Guid id = await CreateProduct(client, "Cleared dress", 75.00m, salePrice: 50.00m);
        (await client.PutAsJsonAsync($"/api/products/{id}", new { status = "sold" })).EnsureSuccessStatusCode();
        Assert.Equal(50.00m, Assert.Single(await RowsFor(client, id), t => t.Category == "Sales").Amount);

        // Zero is how the API clears a sale price.
        (await client.PutAsJsonAsync($"/api/products/{id}", new { salePrice = 0m })).EnsureSuccessStatusCode();

        Assert.Equal(75.00m, Assert.Single(await RowsFor(client, id), t => t.Category == "Sales").Amount);
    }

    [Fact]
    public async Task CorrectingTheListPriceOnASoldPieceWithNoSalePrice_MovesTheLedger()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "marksold-correct-listprice@test.com");

        Guid id = await CreateProduct(client, "Typo dress", 45.00m);
        (await client.PutAsJsonAsync($"/api/products/{id}", new { status = "sold" })).EnsureSuccessStatusCode();

        (await client.PutAsJsonAsync($"/api/products/{id}", new { price = 54.00m })).EnsureSuccessStatusCode();

        Assert.Equal(54.00m, Assert.Single(await RowsFor(client, id), t => t.Category == "Sales").Amount);
    }

    [Fact]
    public async Task Backfill_CorrectsASaleRecordedAtTheWrongAmount()
    {
        // The repair path for rows already stale in prod: a sale logged before the sale price was
        // set keeps the list price, and re-running backfill used to skip it because a sale row
        // existed. Nudging the product price is not something you should have to know to do.
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "marksold-backfill-amount@test.com");

        Guid id = await CreateProduct(client, "Stale amount dress", 100.00m);
        (await client.PutAsJsonAsync($"/api/products/{id}", new { status = "sold" })).EnsureSuccessStatusCode();

        TransactionResponse sale = Assert.Single(await RowsFor(client, id), t => t.Category == "Sales");
        Assert.Equal(100.00m, sale.Amount);

        // Set the sale price straight on the row, bypassing the product update that now syncs it,
        // to recreate a ledger left stale by the old behaviour.
        (await client.PutAsJsonAsync($"/api/finance/{sale.Id}", new { amount = 100.00m }))
            .EnsureSuccessStatusCode();
        (await client.PutAsJsonAsync($"/api/products/{id}", new { salePrice = 70.00m })).EnsureSuccessStatusCode();
        (await client.PutAsJsonAsync($"/api/finance/{sale.Id}", new { amount = 100.00m }))
            .EnsureSuccessStatusCode();
        Assert.Equal(100.00m, Assert.Single(await RowsFor(client, id), t => t.Category == "Sales").Amount);

        (await client.PostAsync("/api/finance/backfill-sales", null)).EnsureSuccessStatusCode();

        Assert.Equal(70.00m, Assert.Single(await RowsFor(client, id), t => t.Category == "Sales").Amount);
    }

    [Fact]
    public async Task Backfill_LeavesACorrectAmountAlone()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "marksold-backfill-noop@test.com");

        Guid id = await CreateProduct(client, "Correct amount dress", 60.00m, salePrice: 42.00m);
        (await client.PutAsJsonAsync($"/api/products/{id}", new { status = "sold" })).EnsureSuccessStatusCode();

        (await client.PostAsync("/api/finance/backfill-sales", null)).EnsureSuccessStatusCode();

        Assert.Equal(42.00m, Assert.Single(await RowsFor(client, id), t => t.Category == "Sales").Amount);
    }

    [Fact]
    public async Task ChangingThePriceOfAPieceThatIsNotSold_TouchesNoLedgerRow()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "marksold-unsold-price@test.com");

        Guid id = await CreateProduct(client, "Still for sale", 40.00m);

        (await client.PutAsJsonAsync($"/api/products/{id}", new { salePrice = 30.00m })).EnsureSuccessStatusCode();

        Assert.DoesNotContain(await RowsFor(client, id), t => t.Category == "Sales");
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
