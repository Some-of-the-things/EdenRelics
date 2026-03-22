using Eden_Relics_BE.Data;
using Eden_Relics_BE.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Eden_Relics_BE.Controllers;

[ApiController]
[Route("api/mailing-list")]
public class MailingListController(EdenRelicsDbContext context) : ControllerBase
{
    [HttpPost("subscribe")]
    public async Task<ActionResult> Subscribe([FromBody] MailingListSubscribeDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Email) || !dto.Email.Contains('@') || dto.Email.Length < 5)
            return BadRequest(new { message = "A valid email address is required." });

        string email = dto.Email.Trim().ToLowerInvariant();

        MailingListSubscriber? existing = await context.MailingListSubscribers
            .FirstOrDefaultAsync(m => m.Email == email);

        if (existing is not null)
        {
            if (existing.Unsubscribed)
            {
                existing.Unsubscribed = false;
                existing.Source = dto.Source ?? "Website";
                await context.SaveChangesAsync();
            }
            return Ok(new { message = "You're on the list!" });
        }

        context.MailingListSubscribers.Add(new MailingListSubscriber
        {
            Email = email,
            FirstName = dto.FirstName?.Trim(),
            Source = dto.Source ?? "Website",
        });
        await context.SaveChangesAsync();

        return Ok(new { message = "You're on the list!" });
    }

    [HttpPost("unsubscribe")]
    public async Task<ActionResult> Unsubscribe([FromBody] MailingListUnsubscribeDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Email))
            return BadRequest(new { message = "Email is required." });

        string email = dto.Email.Trim().ToLowerInvariant();
        MailingListSubscriber? sub = await context.MailingListSubscribers
            .FirstOrDefaultAsync(m => m.Email == email);

        if (sub is not null)
        {
            sub.Unsubscribed = true;
            await context.SaveChangesAsync();
        }

        return Ok(new { message = "You have been unsubscribed." });
    }

    [HttpGet("subscribers")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<List<MailingListSubscriberDto>>> GetAll()
    {
        List<MailingListSubscriber> subs = await context.MailingListSubscribers
            .Where(m => !m.Unsubscribed)
            .OrderByDescending(m => m.CreatedAtUtc)
            .ToListAsync();

        return Ok(subs.Select(s => new MailingListSubscriberDto(
            s.Id, s.Email, s.FirstName, s.Source, s.CreatedAtUtc
        )).ToList());
    }

    [HttpGet("count")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<object>> GetCount()
    {
        int count = await context.MailingListSubscribers.CountAsync(m => !m.Unsubscribed);
        return Ok(new { count });
    }
}

public record MailingListSubscribeDto(string Email, string? FirstName, string? Source);
public record MailingListUnsubscribeDto(string Email);
public record MailingListSubscriberDto(Guid Id, string Email, string? FirstName, string Source, DateTime CreatedAtUtc);
