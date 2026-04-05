using System.Net;
using System.Net.Http.Json;
using static Eden_Relics_BE.Tests.Helpers;

namespace Eden_Relics_BE.Tests;

public class MailingListTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public MailingListTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Subscribe_ValidEmail_Returns200()
    {
        HttpClient client = _factory.CreateClient();
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/mailing-list/subscribe", new
        {
            email = "subscriber@test.com",
            firstName = "Jane",
            source = "Footer"
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        MessageResponse? result = await response.Content.ReadFromJsonAsync<MessageResponse>(JsonOptions);
        Assert.Equal("You're on the list!", result!.Message);
    }

    [Fact]
    public async Task Subscribe_DuplicateEmail_Returns200()
    {
        HttpClient client = _factory.CreateClient();
        await client.PostAsJsonAsync("/api/mailing-list/subscribe", new
        {
            email = "dup-sub@test.com"
        });

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/mailing-list/subscribe", new
        {
            email = "dup-sub@test.com"
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Subscribe_InvalidEmail_Returns400()
    {
        HttpClient client = _factory.CreateClient();
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/mailing-list/subscribe", new
        {
            email = "bad"
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Subscribe_EmptyEmail_Returns400()
    {
        HttpClient client = _factory.CreateClient();
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/mailing-list/subscribe", new
        {
            email = ""
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Unsubscribe_ExistingEmail_Returns200()
    {
        HttpClient client = _factory.CreateClient();
        await client.PostAsJsonAsync("/api/mailing-list/subscribe", new
        {
            email = "will-unsub@test.com"
        });

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/mailing-list/unsubscribe", new
        {
            email = "will-unsub@test.com"
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        MessageResponse? result = await response.Content.ReadFromJsonAsync<MessageResponse>(JsonOptions);
        Assert.Equal("You have been unsubscribed.", result!.Message);
    }

    [Fact]
    public async Task Unsubscribe_NonExistentEmail_Returns200()
    {
        HttpClient client = _factory.CreateClient();
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/mailing-list/unsubscribe", new
        {
            email = "never-subscribed@test.com"
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Unsubscribe_EmptyEmail_Returns400()
    {
        HttpClient client = _factory.CreateClient();
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/mailing-list/unsubscribe", new
        {
            email = ""
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Resubscribe_AfterUnsubscribe_Returns200()
    {
        HttpClient client = _factory.CreateClient();
        await client.PostAsJsonAsync("/api/mailing-list/subscribe", new
        {
            email = "resub@test.com"
        });
        await client.PostAsJsonAsync("/api/mailing-list/unsubscribe", new
        {
            email = "resub@test.com"
        });

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/mailing-list/subscribe", new
        {
            email = "resub@test.com",
            source = "Popup"
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetSubscribers_AsAdmin_ReturnsActiveSubscribers()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "mailing-admin@test.com");

        await client.PostAsJsonAsync("/api/mailing-list/subscribe", new
        {
            email = "active-sub@test.com",
            firstName = "Active"
        });

        await client.PostAsJsonAsync("/api/mailing-list/subscribe", new
        {
            email = "inactive-sub@test.com"
        });
        await client.PostAsJsonAsync("/api/mailing-list/unsubscribe", new
        {
            email = "inactive-sub@test.com"
        });

        List<SubscriberResponse>? subs = await client.GetFromJsonAsync<List<SubscriberResponse>>("/api/mailing-list/subscribers", JsonOptions);
        Assert.NotNull(subs);
        Assert.Contains(subs, s => s.Email == "active-sub@test.com");
        Assert.DoesNotContain(subs, s => s.Email == "inactive-sub@test.com");
    }

    [Fact]
    public async Task GetSubscribers_Unauthenticated_Returns401()
    {
        HttpClient client = _factory.CreateClient();
        HttpResponseMessage response = await client.GetAsync("/api/mailing-list/subscribers");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetSubscribers_AsCustomer_Returns403()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAndLogin(client, "mailing-customer@test.com");
        HttpResponseMessage response = await client.GetAsync("/api/mailing-list/subscribers");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetCount_AsAdmin_ReturnsCount()
    {
        HttpClient client = _factory.CreateClient();
        await RegisterAdmin(client, _factory, "mailing-count@test.com");

        CountResponse? result = await client.GetFromJsonAsync<CountResponse>("/api/mailing-list/count", JsonOptions);
        Assert.NotNull(result);
        Assert.True(result.Count >= 0);
    }

    [Fact]
    public async Task GetCount_Unauthenticated_Returns401()
    {
        HttpClient client = _factory.CreateClient();
        HttpResponseMessage response = await client.GetAsync("/api/mailing-list/count");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private record SubscriberResponse(Guid Id, string Email, string? FirstName, string Source, DateTime CreatedAtUtc);
    private record CountResponse(int Count);
}
