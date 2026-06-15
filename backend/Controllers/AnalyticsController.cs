using System.Security.Cryptography;
using System.Text;
using Eden_Relics_BE.DTOs;
using Eden_Relics_BE.Services;
using Microsoft.AspNetCore.Mvc;

namespace Eden_Relics_BE.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AnalyticsController(
    IAnalyticsIngestService ingest,
    IConfiguration configuration) : ControllerBase
{
    private const string SecretHeader = "X-Analytics-Secret";

    /// <summary>
    /// Cookieless page-view beacon from the Cloudflare Worker (server-to-server).
    /// Secret-gated so counts can't be forged. Returns 204 on success and, deliberately,
    /// 404 when no secret is configured so the endpoint is invisible until switched on.
    /// </summary>
    [HttpPost("pageview")]
    public async Task<IActionResult> PageView([FromBody] PageViewBeaconDto beacon)
    {
        string? configured = configuration["Analytics:IngestSecret"];
        if (string.IsNullOrEmpty(configured))
        {
            return NotFound();
        }

        string provided = Request.Headers[SecretHeader].ToString();
        if (!IsAuthorised(provided, configured))
        {
            return Unauthorized();
        }

        await ingest.RecordPageViewAsync(beacon);
        return NoContent();
    }

    private static bool IsAuthorised(string provided, string configured)
    {
        byte[] providedBytes = Encoding.UTF8.GetBytes(provided);
        byte[] configuredBytes = Encoding.UTF8.GetBytes(configured);
        return CryptographicOperations.FixedTimeEquals(providedBytes, configuredBytes);
    }
}
