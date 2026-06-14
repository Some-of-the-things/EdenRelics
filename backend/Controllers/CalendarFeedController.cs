using System.Security.Cryptography;
using System.Text;
using Eden_Relics_BE.Data.Entities;
using Eden_Relics_BE.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Eden_Relics_BE.Controllers;

/// <summary>
/// Public iCal (RFC 5545) feed of the operator's regulatory obligations. Token-gated via a
/// query string so the URL can be subscribed from Google Calendar / Outlook / iPhone without
/// each client supporting OAuth. The token is the <c>Liabilities:IcalToken</c> config value.
/// With no token configured the endpoint returns 404 — fail-closed.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/calendar")]
public class CalendarFeedController(
    ICalendarFeedService feed,
    IOptions<LiabilityOptions> options) : ControllerBase
{
    private readonly LiabilityOptions _options = options.Value;

    /// <summary>Returns the iCal feed if the supplied token matches the configured one. 404 otherwise.</summary>
    [HttpGet("obligations.ics")]
    [EnableRateLimiting("public-write")]
    public async Task<IActionResult> Feed([FromQuery] string? token, CancellationToken ct)
    {
        string? expected = _options.IcalToken;
        if (string.IsNullOrWhiteSpace(expected) || string.IsNullOrWhiteSpace(token))
        {
            return NotFound();
        }
        if (!FixedTimeEquals(token, expected))
        {
            return NotFound();
        }

        string body = await feed.BuildObligationsFeedAsync(ct);
        // UTF-8 *without* BOM — Outlook desktop refuses to import an .ics file that begins
        // with the BOM bytes (it can't match the leading "BEGIN:VCALENDAR" line).
        return Content(body, "text/calendar; charset=utf-8", new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static bool FixedTimeEquals(string a, string b)
    {
        byte[] x = Encoding.UTF8.GetBytes(a);
        byte[] y = Encoding.UTF8.GetBytes(b);
        return CryptographicOperations.FixedTimeEquals(x, y);
    }
}
