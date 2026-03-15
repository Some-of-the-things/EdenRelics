using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eden_Relics_BE.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class InstagramController(IHttpClientFactory httpClientFactory, IConfiguration configuration) : ControllerBase
{
    [HttpPost("post")]
    public async Task<ActionResult<InstagramPostResult>> Post([FromBody] InstagramPostRequest request)
    {
        string? accessToken = configuration["Instagram:AccessToken"];
        string? userId = configuration["Instagram:UserId"];

        if (string.IsNullOrWhiteSpace(accessToken) || string.IsNullOrWhiteSpace(userId))
            return BadRequest(new { message = "Instagram is not configured. Set Instagram:AccessToken and Instagram:UserId." });

        if (string.IsNullOrWhiteSpace(request.ImageUrl))
            return BadRequest(new { message = "Image URL is required." });

        if (string.IsNullOrWhiteSpace(request.Caption))
            return BadRequest(new { message = "Caption is required." });

        HttpClient client = httpClientFactory.CreateClient();

        try
        {
            // Step 1: Create media container
            var createResponse = await client.PostAsync(
                $"https://graph.facebook.com/v21.0/{userId}/media?image_url={Uri.EscapeDataString(request.ImageUrl)}&caption={Uri.EscapeDataString(request.Caption)}&access_token={accessToken}",
                null);

            string createJson = await createResponse.Content.ReadAsStringAsync();

            if (!createResponse.IsSuccessStatusCode)
            {
                string error = TryExtractError(createJson);
                return BadRequest(new { message = $"Failed to create media container: {error}" });
            }

            using JsonDocument createDoc = JsonDocument.Parse(createJson);
            string containerId = createDoc.RootElement.GetProperty("id").GetString()!;

            // Step 2: Publish the container
            var publishResponse = await client.PostAsync(
                $"https://graph.facebook.com/v21.0/{userId}/media_publish?creation_id={containerId}&access_token={accessToken}",
                null);

            string publishJson = await publishResponse.Content.ReadAsStringAsync();

            if (!publishResponse.IsSuccessStatusCode)
            {
                string error = TryExtractError(publishJson);
                return BadRequest(new { message = $"Failed to publish: {error}" });
            }

            using JsonDocument publishDoc = JsonDocument.Parse(publishJson);
            string mediaId = publishDoc.RootElement.GetProperty("id").GetString()!;

            return Ok(new InstagramPostResult(mediaId, "Posted successfully."));
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = $"Instagram API error: {ex.Message}" });
        }
    }

    [HttpGet("status")]
    public ActionResult<object> Status()
    {
        string? accessToken = configuration["Instagram:AccessToken"];
        string? userId = configuration["Instagram:UserId"];
        bool configured = !string.IsNullOrWhiteSpace(accessToken) && !string.IsNullOrWhiteSpace(userId);
        return Ok(new { configured });
    }

    private static string TryExtractError(string json)
    {
        try
        {
            using JsonDocument doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("error", out JsonElement err) &&
                err.TryGetProperty("message", out JsonElement msg))
                return msg.GetString() ?? json;
        }
        catch { }
        return json;
    }
}

public record InstagramPostRequest(string ImageUrl, string Caption);
public record InstagramPostResult(string MediaId, string Message);
