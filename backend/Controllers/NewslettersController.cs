using Eden_Relics_BE.DTOs;
using Eden_Relics_BE.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Eden_Relics_BE.Controllers;

[ApiController]
[Route("api/newsletters")]
[Authorize(Roles = "Admin")]
[NewsletterValidationFilter]
public class NewslettersController(INewsletterService newsletters, IEmailImageService emailImages) : ControllerBase
{
    /// <summary>A JPEG copy of a product photo for a newsletter (Outlook on Windows can't show WebP).</summary>
    [HttpPost("email-image")]
    public async Task<ActionResult<EmailImageResponse>> EmailImage([FromBody] EmailImageRequest request, CancellationToken ct)
    {
        string? url = await emailImages.ToEmailJpegAsync(request.Url ?? string.Empty, ct);
        return url is null ? BadRequest(new { error = "That image can't be used." }) : Ok(new EmailImageResponse(url));
    }

    [HttpGet]
    public async Task<ActionResult<List<NewsletterDto>>> List()
    {
        return Ok(await newsletters.ListAsync());
    }

    [HttpGet("audience")]
    public async Task<ActionResult<NewsletterAudienceDto>> Audience()
    {
        return Ok(new NewsletterAudienceDto(await newsletters.GetAudienceCountAsync()));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<NewsletterDto>> Get(Guid id)
    {
        NewsletterDto? n = await newsletters.GetAsync(id);
        return n is null ? NotFound() : Ok(n);
    }

    [HttpPost]
    public async Task<ActionResult<NewsletterDto>> Create([FromBody] SaveNewsletterRequest request)
    {
        return Ok(await newsletters.CreateAsync(request));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<NewsletterDto>> Update(Guid id, [FromBody] SaveNewsletterRequest request)
    {
        NewsletterDto? n = await newsletters.UpdateAsync(id, request);
        return n is null ? NotFound() : Ok(n);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        return await newsletters.DeleteAsync(id) ? NoContent() : NotFound();
    }

    [HttpPost("preview")]
    public async Task<ActionResult<NewsletterPreviewDto>> Preview([FromBody] NewsletterPreviewRequest request)
    {
        return Ok(await newsletters.PreviewAsync(request));
    }

    [HttpPost("{id:guid}/test")]
    public async Task<IActionResult> Test(Guid id, [FromBody] NewsletterTestRequest request)
    {
        bool ok = await newsletters.SendTestAsync(id, request.Email ?? string.Empty);
        return ok ? Ok(new { message = "Test sent." }) : NotFound();
    }

    [HttpPost("{id:guid}/send")]
    public async Task<ActionResult<NewsletterDto>> Send(Guid id, [FromBody] SendNewsletterRequest request)
    {
        NewsletterDto? n = await newsletters.SendAsync(id, request.ScheduledForUtc);
        return n is null ? NotFound() : Ok(n);
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<NewsletterDto>> Cancel(Guid id)
    {
        NewsletterDto? n = await newsletters.CancelScheduleAsync(id);
        return n is null ? NotFound() : Ok(n);
    }
}

/// <summary>Turns a <see cref="NewsletterValidationException"/> into a 400 with its message.</summary>
public sealed class NewsletterValidationFilterAttribute : ExceptionFilterAttribute
{
    public override void OnException(ExceptionContext context)
    {
        if (context.Exception is NewsletterValidationException ex)
        {
            context.Result = new BadRequestObjectResult(new { error = ex.Message });
            context.ExceptionHandled = true;
        }
    }
}
