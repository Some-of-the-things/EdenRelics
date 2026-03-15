using Eden_Relics_BE.DTOs;
using Eden_Relics_BE.Services;
using Microsoft.AspNetCore.Mvc;

namespace Eden_Relics_BE.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ContactController(IEmailService emailService) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Submit([FromBody] ContactDto dto)
    {
        await emailService.SendContactEmailAsync(dto.Name, dto.Email, dto.Subject, dto.Message);
        return Ok();
    }
}
