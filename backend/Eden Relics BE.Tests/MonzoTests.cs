using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using static Eden_Relics_BE.Tests.Helpers;

namespace Eden_Relics_BE.Tests;

public class MonzoTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public MonzoTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetStatus_AsAdmin_ReturnsNotConnected()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "monzo-status@test.com");

        HttpResponseMessage response = await client.GetAsync("/api/monzo/status");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        string json = await response.Content.ReadAsStringAsync();
        JsonElement status = JsonDocument.Parse(json).RootElement;
        Assert.False(status.GetProperty("connected").GetBoolean());
    }

    [Fact]
    public async Task Connect_NotConfigured_Returns400()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "monzo-connect@test.com");

        HttpResponseMessage response = await client.GetAsync("/api/monzo/connect");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Disconnect_AsAdmin_Returns200()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "monzo-disconnect@test.com");

        HttpResponseMessage response = await client.PostAsync("/api/monzo/disconnect", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetTransactions_AsAdmin_ReturnsEmptyList()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "monzo-txns@test.com");

        List<MonzoTxnResponse>? transactions = await client.GetFromJsonAsync<List<MonzoTxnResponse>>("/api/monzo/transactions", JsonOptions);
        Assert.NotNull(transactions);
        Assert.Empty(transactions);
    }

    [Fact]
    public async Task GetSummary_AsAdmin_ReturnsEmptyTotals()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "monzo-summary@test.com");

        HttpResponseMessage response = await client.GetAsync("/api/monzo/summary");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        string json = await response.Content.ReadAsStringAsync();
        JsonElement summary = JsonDocument.Parse(json).RootElement;
        Assert.Equal(0, summary.GetProperty("transactionCount").GetInt32());
        Assert.Equal(0m, summary.GetProperty("totalIncome").GetDecimal());
    }

    [Fact]
    public async Task Export_AsAdmin_ReturnsCsv()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "monzo-export@test.com");

        HttpResponseMessage response = await client.GetAsync("/api/monzo/export");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType);

        string csv = await response.Content.ReadAsStringAsync();
        Assert.StartsWith("Date,Description,Amount,Monzo Category,Tagged Category,Platform,Merchant,Notes,Settled,Receipt", csv);
    }

    [Fact]
    public async Task Verify_NoToken_Returns400()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "monzo-verify@test.com");

        HttpResponseMessage response = await client.PostAsync("/api/monzo/verify", null);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Sync_NotConnected_Returns400()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "monzo-sync@test.com");

        HttpResponseMessage response = await client.PostAsync("/api/monzo/sync", null);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetBalance_NotConnected_Returns400()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "monzo-balance@test.com");

        HttpResponseMessage response = await client.GetAsync("/api/monzo/balance");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetAccounts_NotConnected_Returns400()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "monzo-accounts@test.com");

        HttpResponseMessage response = await client.GetAsync("/api/monzo/accounts");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Annotate_NonExistent_Returns404()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "monzo-annotate@test.com");

        HttpResponseMessage response = await client.PatchAsJsonAsync($"/api/monzo/transactions/{Guid.Empty}/annotate", new
        {
            notes = "test"
        });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AllEndpoints_Unauthenticated_Returns401()
    {
        HttpClient client = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/monzo/status")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/monzo/transactions")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/monzo/summary")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/api/monzo/disconnect", null)).StatusCode);
    }

    [Fact]
    public async Task AllEndpoints_AsCustomer_Returns403()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAndLogin(client, "monzo-customer@test.com");

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/monzo/status")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/monzo/transactions")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync("/api/monzo/disconnect", null)).StatusCode);
    }

    private record MonzoTxnResponse(Guid Id, string MonzoId, DateTime Date, string Description, decimal Amount);
}
