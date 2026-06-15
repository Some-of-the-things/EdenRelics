using Eden_Relics_BE.DTOs;
using Eden_Relics_BE.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eden_Relics_BE.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class OffsiteSalesController(IOffsiteSaleService sales) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<OffsiteSaleDto>>> GetAll()
    {
        return Ok(await sales.GetAllAsync());
    }

    [HttpPost]
    public async Task<ActionResult<OffsiteSaleDto>> Create(CreateOffsiteSaleDto dto)
    {
        return Ok(await sales.CreateAsync(dto));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<OffsiteSaleDto>> Update(Guid id, CreateOffsiteSaleDto dto)
    {
        OffsiteSaleDto? sale = await sales.UpdateAsync(id, dto);
        return sale is null ? NotFound() : Ok(sale);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        bool deleted = await sales.DeleteAsync(id);
        return deleted ? NoContent() : NotFound();
    }
}
