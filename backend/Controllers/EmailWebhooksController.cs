using Eden_Relics_BE.Services;
using Microsoft.AspNetCore.Mvc;

namespace Eden_Relics_BE.Controllers;

/// <summary>Delivery events from Resend (opens, clicks, bounces, complaints) for the newsletter figures.</summary>
[ApiController]
[Route("api/webhooks")]
public class EmailWebhooksController(INewsletterTrackingService tracking) : ControllerBase
{
    [HttpPost("resend")]
    [RequestSizeLimit(256 * 1024)]
    public async Task<IActionResult> Resend()
    {
        using StreamReader reader = new(Request.Body);
        string body = await reader.ReadToEndAsync();
        if (!tracking.Verify(
                Request.Headers["svix-id"].FirstOrDefault(),
                Request.Headers["svix-timestamp"].FirstOrDefault(),
                Request.Headers["svix-signature"].FirstOrDefault(),
                body,
                DateTime.UtcNow))
        {
            return Unauthorized();
        }
        await tracking.HandleAsync(body);
        return Ok();
    }
}
