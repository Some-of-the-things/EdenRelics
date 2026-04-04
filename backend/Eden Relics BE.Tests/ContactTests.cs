using System.Net;
using System.Net.Http.Json;
using static Eden_Relics_BE.Tests.Helpers;

namespace Eden_Relics_BE.Tests;

public class ContactTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public ContactTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Submit_ValidMessage_Returns200()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/contact", new
        {
            name = "Jane Doe",
            email = "jane@example.com",
            subject = "Question about a dress",
            message = "Is the silk slip dress still available in a size 10?"
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<MessageResponse>(JsonOptions);
        Assert.NotNull(result);
        Assert.Equal("Message sent successfully.", result.Message);
    }

    [Fact]
    public async Task Submit_MissingName_Returns400()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/contact", new
        {
            name = (string?)null,
            email = "jane@example.com",
            subject = "Test",
            message = "Test message"
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Submit_InvalidEmail_Returns400()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/contact", new
        {
            name = "Jane",
            email = "not-an-email",
            subject = "Test",
            message = "Test message"
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Submit_MissingMessage_Returns400()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/contact", new
        {
            name = "Jane",
            email = "jane@example.com",
            subject = "Test",
            message = (string?)null
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
