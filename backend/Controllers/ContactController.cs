using Eden_Relics_BE.DTOs;
using Eden_Relics_BE.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Eden_Relics_BE.Controllers;

[ApiController]
[Route("api/[controller]")]
[EnableRateLimiting("contact")]
public class ContactController(IEmailService emailService, ILogger<ContactController> logger) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Submit([FromBody] ContactDto dto)
    {
        try
        {
            await emailService.SendContactEmailAsync(dto.Name, dto.Email, dto.Subject, dto.Message);
            return Ok(new { message = "Message sent successfully." });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send contact form email");
            return StatusCode(503, new { message = "Our email service is temporarily unavailable. Please try again later or contact us directly." });
        }
    }
}
