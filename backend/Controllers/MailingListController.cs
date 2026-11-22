using System.Globalization;
using System.Text;
using Eden_Relics_BE.DTOs;
using Eden_Relics_BE.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Eden_Relics_BE.Controllers;

[ApiController]
[Route("api/mailing-list")]
public class MailingListController(IMailingListService mailingList) : ControllerBase
{
    [HttpPost("subscribe")]
    [EnableRateLimiting("contact")]
    public async Task<ActionResult> Subscribe([FromBody] MailingListSubscribeDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Email) || !await mailingList.SubscribeAsync(dto))
        {
            return BadRequest(new { message = "A valid email address is required." });
        }
        return Ok(new { message = "You're on the list!" });
    }

    [HttpPost("unsubscribe")]
    [EnableRateLimiting("contact")]
    public async Task<ActionResult> Unsubscribe([FromBody] MailingListUnsubscribeDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Email))
        {
            return BadRequest(new { message = "Email is required." });
        }

        await mailingList.UnsubscribeAsync(dto.Email);
        return Ok(new { message = "You have been unsubscribed." });
    }

    /// <summary>
    /// RFC 8058 one-click unsubscribe, the target of every newsletter's List-Unsubscribe header.
    /// Mail providers POST here with no cookies or credentials; it must succeed without a page.
    /// </summary>
    [HttpPost("one-click/{token}")]
    [IgnoreAntiforgeryToken]
    public async Task<ActionResult> OneClick(string token)
    {
        await mailingList.UnsubscribeByTokenAsync(token);
        // Same answer either way: don't reveal which tokens exist.
        return Ok(new { message = "You have been unsubscribed." });
    }

    /// <summary>The unsubscribe page (from the link in the email footer).</summary>
    [HttpPost("unsubscribe-token")]
    [EnableRateLimiting("contact")]
    public async Task<ActionResult> UnsubscribeByToken([FromBody] UnsubscribeTokenRequest request)
    {
        bool ok = await mailingList.UnsubscribeByTokenAsync(request.Token ?? string.Empty);
        return ok ? Ok(new { message = "You have been unsubscribed." }) : NotFound(new { message = "This unsubscribe link isn't valid." });
    }

    [HttpPost("resubscribe-token")]
    [EnableRateLimiting("contact")]
    public async Task<ActionResult> ResubscribeByToken([FromBody] UnsubscribeTokenRequest request)
    {
        bool ok = await mailingList.ResubscribeByTokenAsync(request.Token ?? string.Empty);
        return ok ? Ok(new { message = "You're back on the list." }) : NotFound(new { message = "This link isn't valid." });
    }

    [HttpGet("subscribers")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<List<MailingListSubscriberDto>>> GetAll()
    {
        return Ok(await mailingList.GetActiveAsync());
    }

    [HttpGet("count")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<object>> GetCount()
    {
        int count = await mailingList.GetActiveCountAsync();
        return Ok(new { count });
    }

    [HttpGet("admin/subscribers")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<List<AdminSubscriberDto>>> GetForAdmin([FromQuery] string? search, [FromQuery] string? status)
    {
        return Ok(await mailingList.GetForAdminAsync(search, status));
    }

    [HttpGet("admin/subscribers.csv")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ExportCsv([FromQuery] string? status)
    {
        List<AdminSubscriberDto> rows = await mailingList.GetForAdminAsync(null, status);
        StringBuilder csv = new();
        csv.AppendLine("Email,First name,Source,Signed up (UTC),Status,Unsubscribed (UTC)");
        foreach (AdminSubscriberDto r in rows)
        {
            csv.AppendLine(string.Join(',',
                Csv(r.Email),
                Csv(r.FirstName),
                Csv(r.Source),
                r.SignedUpAtUtc.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
                r.Unsubscribed ? "Unsubscribed" : "Active",
                r.UnsubscribedAtUtc?.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) ?? string.Empty));
        }
        byte[] bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
        return File(bytes, "text/csv", $"subscribers-{DateTime.UtcNow:yyyy-MM-dd}.csv");
    }

    [HttpPost("admin/subscribers/{id:guid}/unsubscribe")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> MarkUnsubscribed(Guid id)
    {
        return await mailingList.MarkUnsubscribedAsync(id) ? NoContent() : NotFound();
    }

    [HttpDelete("admin/subscribers/{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Remove(Guid id)
    {
        return await mailingList.RemoveAsync(id) ? NoContent() : NotFound();
    }

    /// <summary>Quotes a CSV field, and stops a spreadsheet treating it as a formula.</summary>
    private static string Csv(string? value)
    {
        string v = value ?? string.Empty;
        if (v.Length > 0 && "=+-@\t\r".Contains(v[0]))
        {
            v = "'" + v;
        }
        return $"\"{v.Replace("\"", "\"\"")}\"";
    }
}
