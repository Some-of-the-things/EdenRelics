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
        if (string.IsNullOrWhiteSpace(dto.Email) || !dto.Email.Contains('@') || dto.Email.Length < 5)
        {
            return BadRequest(new { message = "A valid email address is required." });
        }

        await mailingList.SubscribeAsync(dto);
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
}
