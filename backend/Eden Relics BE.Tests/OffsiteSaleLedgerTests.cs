using System.Net.Http.Json;
using System.Text.Json;
using static Eden_Relics_BE.Tests.Helpers;

namespace Eden_Relics_BE.Tests;

/// <summary>
/// Offsite sales must reach the Finance ledger. Recording one used to write only an OffsiteSale
/// row, so the Finance tab's total income silently ignored every sale made on Vinted/Depop/eBay
/// — reported 2026-08-19.
/// </summary>
public class OffsiteSaleLedgerTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public OffsiteSaleLedgerTests(ApiFactory factory)
    {
        _factory = factory;
    }

    private static object SalePayload(
        string name = "1970s Prairie Dress",
        decimal salePrice = 48.00m,
        decimal costPrice = 12.00m,
        string platform = "Vinted") => new
        {
            dressName = name,
            era = "1970s",
            category = "Dresses",
            size = "M",
            condition = "Good",
            salePrice,
            costPrice,
            platform,
            saleDateUtc = new DateTime(2026, 7, 4, 12, 0, 0, DateTimeKind.Utc),
        };

    private static async Task<List<TransactionResponse>> RowsForSale(HttpClient client, Guid saleId)
    {
        List<TransactionResponse>? all =
            await client.GetFromJsonAsync<List<TransactionResponse>>("/api/finance", JsonOptions);
        string income = $"offsite:{saleId}";
        string cogs = $"offsite-cogs:{saleId}";
        return [.. all!.Where(t => t.Reference == income || t.Reference == cogs)];
    }

    private static async Task<Guid> CreateSale(HttpClient client, object payload)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/offsitesales", payload);
        response.EnsureSuccessStatusCode();
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task RecordingAnOffsiteSale_AddsItToTheFinanceLedger()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "offsite-ledger-create@test.com");

        Guid saleId = await CreateSale(client, SalePayload(salePrice: 48.00m, costPrice: 12.00m));

        List<TransactionResponse> rows = await RowsForSale(client, saleId);

        TransactionResponse income = Assert.Single(rows, t => t.Category == "Sales");
        Assert.Equal(48.00m, income.Amount);
        // The platform is what puts it under the Finance tab's "external" source filter.
        Assert.Equal("Vinted", income.Platform);

        TransactionResponse cogs = Assert.Single(rows, t => t.Category == "Stock");
        Assert.Equal(-12.00m, cogs.Amount);
    }

    [Fact]
    public async Task TheLedgerUsesThePriceItActuallySoldFor()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "offsite-ledger-price@test.com");

        Guid saleId = await CreateSale(client, SalePayload(salePrice: 33.50m, costPrice: 0m));

        List<TransactionResponse> rows = await RowsForSale(client, saleId);
        Assert.Equal(33.50m, Assert.Single(rows, t => t.Category == "Sales").Amount);
    }

    [Fact]
    public async Task IncomeAndCostLandInTheMonthThePieceSold()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "offsite-ledger-date@test.com");

        Guid saleId = await CreateSale(client, SalePayload());

        List<TransactionResponse> rows = await RowsForSale(client, saleId);
        Assert.Equal(2, rows.Count);
        Assert.All(rows, t => Assert.Equal(new DateTime(2026, 7, 4), t.Date.Date));
    }

    [Fact]
    public async Task CorrectingTheSalePrice_MovesTheLedgerWithIt()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "offsite-ledger-update@test.com");

        Guid saleId = await CreateSale(client, SalePayload(salePrice: 48.00m));

        (await client.PutAsJsonAsync($"/api/offsitesales/{saleId}", SalePayload(salePrice: 55.00m)))
            .EnsureSuccessStatusCode();

        List<TransactionResponse> rows = await RowsForSale(client, saleId);
        // Corrected, not duplicated.
        Assert.Equal(55.00m, Assert.Single(rows, t => t.Category == "Sales").Amount);
    }

    [Fact]
    public async Task ClearingTheCostPrice_RemovesTheCostRow()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "offsite-ledger-nocost@test.com");

        Guid saleId = await CreateSale(client, SalePayload(costPrice: 12.00m));
        Assert.Contains(await RowsForSale(client, saleId), t => t.Category == "Stock");

        (await client.PutAsJsonAsync($"/api/offsitesales/{saleId}", SalePayload(costPrice: 0m)))
            .EnsureSuccessStatusCode();

        Assert.DoesNotContain(await RowsForSale(client, saleId), t => t.Category == "Stock");
    }

    [Fact]
    public async Task DeletingTheSale_StopsItCountingAsIncome()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "offsite-ledger-delete@test.com");

        Guid saleId = await CreateSale(client, SalePayload());
        Assert.NotEmpty(await RowsForSale(client, saleId));

        (await client.DeleteAsync($"/api/offsitesales/{saleId}")).EnsureSuccessStatusCode();

        Assert.Empty(await RowsForSale(client, saleId));
    }

    [Fact]
    public async Task Backfill_RepairsOffsiteSalesRecordedBeforeTheLedgerKnewAboutThem()
    {
        // The recovery path for prod: sales already in the table with no ledger rows at all.
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "offsite-ledger-backfill@test.com");

        Guid saleId = await CreateSale(client, SalePayload(salePrice: 64.00m, costPrice: 20.00m));

        // Put it in the state prod is in: the sale exists, the ledger rows do not.
        foreach (TransactionResponse row in await RowsForSale(client, saleId))
        {
            (await client.DeleteAsync($"/api/finance/{row.Id}")).EnsureSuccessStatusCode();
        }
        Assert.Empty(await RowsForSale(client, saleId));

        (await client.PostAsync("/api/finance/backfill-sales", null)).EnsureSuccessStatusCode();

        List<TransactionResponse> rows = await RowsForSale(client, saleId);
        Assert.Equal(64.00m, Assert.Single(rows, t => t.Category == "Sales").Amount);
        Assert.Equal(-20.00m, Assert.Single(rows, t => t.Category == "Stock").Amount);
    }

    [Fact]
    public async Task Backfill_IsSafeToRunTwice()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "offsite-ledger-backfill-twice@test.com");

        Guid saleId = await CreateSale(client, SalePayload());

        (await client.PostAsync("/api/finance/backfill-sales", null)).EnsureSuccessStatusCode();
        (await client.PostAsync("/api/finance/backfill-sales", null)).EnsureSuccessStatusCode();

        List<TransactionResponse> rows = await RowsForSale(client, saleId);
        Assert.Single(rows, t => t.Category == "Sales");
        Assert.Single(rows, t => t.Category == "Stock");
    }

    [Fact]
    public async Task TheSaleCountsTowardsTotalIncome()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "offsite-ledger-income@test.com");

        FinanceSummaryResponse? before =
            await client.GetFromJsonAsync<FinanceSummaryResponse>("/api/finance/summary", JsonOptions);

        await CreateSale(client, SalePayload(salePrice: 40.00m, costPrice: 0m));

        FinanceSummaryResponse? after =
            await client.GetFromJsonAsync<FinanceSummaryResponse>("/api/finance/summary", JsonOptions);

        Assert.Equal(before!.TotalIncome + 40.00m, after!.TotalIncome);
    }
}
