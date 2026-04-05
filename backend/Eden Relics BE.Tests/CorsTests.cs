using System.Net;

namespace Eden_Relics_BE.Tests;

/// <summary>
/// Tests that verify CORS headers are correctly set on responses.
/// These catch configuration issues that would block the frontend
/// from making API calls in production.
/// </summary>
public class CorsTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public CorsTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task PreflightRequest_WithAllowedOrigin_ReturnsCorrectHeaders()
    {
        HttpClient client = _factory.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Options, "/api/products");
        request.Headers.Add("Origin", "http://localhost:4200");
        request.Headers.Add("Access-Control-Request-Method", "GET");
        request.Headers.Add("Access-Control-Request-Headers", "Authorization, Content-Type");

        HttpResponseMessage response = await client.SendAsync(request);

        // Preflight should return 204 No Content (or 200)
        Assert.True(response.StatusCode is HttpStatusCode.NoContent or HttpStatusCode.OK,
            $"Preflight returned {response.StatusCode}");

        // Must have Access-Control-Allow-Origin
        Assert.True(response.Headers.Contains("Access-Control-Allow-Origin"),
            "Missing Access-Control-Allow-Origin header");
        string allowOrigin = response.Headers.GetValues("Access-Control-Allow-Origin").First();
        Assert.Equal("http://localhost:4200", allowOrigin);
    }

    [Fact]
    public async Task PreflightRequest_WithDisallowedOrigin_DoesNotReturnCorsHeaders()
    {
        HttpClient client = _factory.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Options, "/api/products");
        request.Headers.Add("Origin", "https://evil-site.com");
        request.Headers.Add("Access-Control-Request-Method", "GET");

        HttpResponseMessage response = await client.SendAsync(request);

        // Should NOT have Access-Control-Allow-Origin for disallowed origin
        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"),
            "Should not return CORS headers for disallowed origin");
    }

    [Fact]
    public async Task SimpleRequest_WithAllowedOrigin_ReturnsAccessControlHeader()
    {
        HttpClient client = _factory.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/products");
        request.Headers.Add("Origin", "http://localhost:4200");

        HttpResponseMessage response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.Contains("Access-Control-Allow-Origin"),
            "Missing Access-Control-Allow-Origin on simple request");
    }

    [Fact]
    public async Task PreflightRequest_AllowsAuthorizationHeader()
    {
        HttpClient client = _factory.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Options, "/api/account/profile");
        request.Headers.Add("Origin", "http://localhost:4200");
        request.Headers.Add("Access-Control-Request-Method", "GET");
        request.Headers.Add("Access-Control-Request-Headers", "Authorization");

        HttpResponseMessage response = await client.SendAsync(request);

        Assert.True(response.Headers.Contains("Access-Control-Allow-Headers"),
            "Missing Access-Control-Allow-Headers");
    }

    [Fact]
    public async Task PreflightRequest_AllowsPutAndDeleteMethods()
    {
        HttpClient client = _factory.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Options, "/api/products");
        request.Headers.Add("Origin", "http://localhost:4200");
        request.Headers.Add("Access-Control-Request-Method", "PUT");

        HttpResponseMessage response = await client.SendAsync(request);

        Assert.True(response.Headers.Contains("Access-Control-Allow-Methods"),
            "Missing Access-Control-Allow-Methods");
        string methods = response.Headers.GetValues("Access-Control-Allow-Methods").First();
        Assert.Contains("PUT", methods);
    }
}
