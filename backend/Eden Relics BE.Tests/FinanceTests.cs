using System.Net;
using System.Net.Http.Json;
using static Eden_Relics_BE.Tests.Helpers;

namespace Eden_Relics_BE.Tests;

public class FinanceTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public FinanceTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetAll_Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/finance");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_AsNonAdmin_Returns403()
    {
        var client = _factory.CreateClient();
        await RegisterAndLogin(client, "fin-user@test.com");
        var response = await client.GetAsync("/api/finance");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_AsAdmin_ReturnsEmptyList()
    {
        var client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "fin-admin-getall@test.com");
        var transactions = await client.GetFromJsonAsync<List<TransactionResponse>>("/api/finance", JsonOptions);
        Assert.NotNull(transactions);
    }

    [Fact]
    public async Task Create_AsAdmin_ReturnsCreatedTransaction()
    {
        var client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "fin-admin-create@test.com");

        var response = await client.PostAsJsonAsync("/api/finance", new
        {
            date = "2026-03-15",
            description = "Stock purchase - vintage dresses",
            amount = -45.50m,
            category = "Stock",
            platform = "eBay",
            reference = "INV-001",
            notes = "Batch of 3 dresses"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var txn = await response.Content.ReadFromJsonAsync<TransactionResponse>(JsonOptions);
        Assert.NotNull(txn);
        Assert.Equal("Stock purchase - vintage dresses", txn.Description);
        Assert.Equal(-45.50m, txn.Amount);
        Assert.Equal("Stock", txn.Category);
        Assert.Equal("eBay", txn.Platform);
        Assert.Equal("INV-001", txn.Reference);
        Assert.Equal("Batch of 3 dresses", txn.Notes);
    }

    [Fact]
    public async Task Create_Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/finance", new
        {
            date = "2026-03-15",
            description = "Test",
            amount = -10m,
            category = "Stock"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Update_AsAdmin_ReturnsUpdatedTransaction()
    {
        var client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "fin-admin-update@test.com");

        // Create
        var createResponse = await client.PostAsJsonAsync("/api/finance", new
        {
            date = "2026-03-10",
            description = "Shipping supplies",
            amount = -12.00m,
            category = "Shipping"
        });
        var created = await createResponse.Content.ReadFromJsonAsync<TransactionResponse>(JsonOptions);

        // Update
        var updateResponse = await client.PutAsJsonAsync($"/api/finance/{created!.Id}", new
        {
            description = "Shipping supplies (corrected)",
            amount = -15.00m,
            category = "Packaging",
            platform = "Website"
        });

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = await updateResponse.Content.ReadFromJsonAsync<TransactionResponse>(JsonOptions);
        Assert.NotNull(updated);
        Assert.Equal("Shipping supplies (corrected)", updated.Description);
        Assert.Equal(-15.00m, updated.Amount);
        Assert.Equal("Packaging", updated.Category);
        Assert.Equal("Website", updated.Platform);
    }

    [Fact]
    public async Task Update_NonExistent_Returns404()
    {
        var client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "fin-admin-update404@test.com");

        var response = await client.PutAsJsonAsync($"/api/finance/{Guid.Empty}", new
        {
            description = "Should not exist"
        });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_AsAdmin_Returns204()
    {
        var client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "fin-admin-delete@test.com");

        // Create
        var createResponse = await client.PostAsJsonAsync("/api/finance", new
        {
            date = "2026-03-01",
            description = "To be deleted",
            amount = -5.00m,
            category = "Other"
        });
        var created = await createResponse.Content.ReadFromJsonAsync<TransactionResponse>(JsonOptions);

        // Delete
        var deleteResponse = await client.DeleteAsync($"/api/finance/{created!.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        // Verify soft-deleted (should not appear in list)
        var transactions = await client.GetFromJsonAsync<List<TransactionResponse>>("/api/finance", JsonOptions);
        Assert.DoesNotContain(transactions!, t => t.Id == created.Id);
    }

    [Fact]
    public async Task Delete_NonExistent_Returns404()
    {
        var client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "fin-admin-delete404@test.com");
        var response = await client.DeleteAsync($"/api/finance/{Guid.Empty}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Summary_AsAdmin_ReturnsCorrectTotals()
    {
        var client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "fin-admin-summary@test.com");

        // Create income and expense transactions
        await client.PostAsJsonAsync("/api/finance", new
        {
            date = "2026-04-01",
            description = "Dress sale",
            amount = 85.00m,
            category = "Sales",
            platform = "Website"
        });

        await client.PostAsJsonAsync("/api/finance", new
        {
            date = "2026-04-02",
            description = "Packaging materials",
            amount = -20.00m,
            category = "Packaging"
        });

        await client.PostAsJsonAsync("/api/finance", new
        {
            date = "2026-04-03",
            description = "Etsy sale",
            amount = 65.00m,
            category = "Sales",
            platform = "Etsy"
        });

        var summary = await client.GetFromJsonAsync<FinanceSummaryResponse>("/api/finance/summary", JsonOptions);
        Assert.NotNull(summary);
        Assert.True(summary.TotalIncome >= 150.00m, $"Expected income >= 150, got {summary.TotalIncome}");
        Assert.True(summary.TotalExpenses >= 20.00m, $"Expected expenses >= 20, got {summary.TotalExpenses}");
        Assert.Equal(summary.TotalProfit, summary.TotalIncome - summary.TotalExpenses);
        Assert.True(summary.TransactionCount >= 3, $"Expected >= 3 transactions, got {summary.TransactionCount}");
        Assert.NotEmpty(summary.ByMonth);
    }

    [Fact]
    public async Task Summary_Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/finance/summary");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Export_AsAdmin_ReturnsCsv()
    {
        var client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "fin-admin-export@test.com");

        // Create a transaction so there's data to export
        await client.PostAsJsonAsync("/api/finance", new
        {
            date = "2026-04-01",
            description = "Export test item",
            amount = -30.00m,
            category = "Stock"
        });

        var response = await client.GetAsync("/api/finance/export");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType);

        var csv = await response.Content.ReadAsStringAsync();
        Assert.StartsWith("Date,Description,Amount,Category,Platform,Reference,Notes", csv);
        Assert.Contains("Export test item", csv);
    }

    [Fact]
    public async Task Export_Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/finance/export");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_FilterByYearAndMonth_FiltersCorrectly()
    {
        var client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "fin-admin-filter@test.com");

        await client.PostAsJsonAsync("/api/finance", new
        {
            date = "2026-01-15",
            description = "January item",
            amount = -10.00m,
            category = "Stock"
        });

        await client.PostAsJsonAsync("/api/finance", new
        {
            date = "2026-02-15",
            description = "February item",
            amount = -20.00m,
            category = "Stock"
        });

        var janOnly = await client.GetFromJsonAsync<List<TransactionResponse>>("/api/finance?year=2026&month=1", JsonOptions);
        Assert.NotNull(janOnly);
        Assert.All(janOnly, t => Assert.Equal(1, t.Date.Month));
        Assert.Contains(janOnly, t => t.Description == "January item");
        Assert.DoesNotContain(janOnly, t => t.Description == "February item");
    }
}
