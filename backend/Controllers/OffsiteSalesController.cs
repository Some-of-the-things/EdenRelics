using Eden_Relics_BE.Data;
using Eden_Relics_BE.Data.Entities;
using Eden_Relics_BE.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Eden_Relics_BE.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class OffsiteSalesController(EdenRelicsDbContext context) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<OffsiteSaleDto>>> GetAll()
    {
        List<OffsiteSale> sales = await context.OffsiteSales
            .OrderByDescending(s => s.SaleDateUtc)
            .ToListAsync();
        return Ok(sales.Select(ToDto).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<OffsiteSaleDto>> Create(CreateOffsiteSaleDto dto)
    {
        OffsiteSale sale = new()
        {
            DressName = dto.DressName.Trim(),
            Era = dto.Era.Trim(),
            Category = dto.Category.Trim(),
            Size = dto.Size.Trim(),
            Condition = dto.Condition.Trim(),
            SalePrice = dto.SalePrice,
            CostPrice = dto.CostPrice,
            Platform = dto.Platform.Trim(),
            SaleDateUtc = DateTime.SpecifyKind(dto.SaleDateUtc, DateTimeKind.Utc),
            Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim(),
        };
        context.OffsiteSales.Add(sale);
        await context.SaveChangesAsync();
        return Ok(ToDto(sale));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<OffsiteSaleDto>> Update(Guid id, CreateOffsiteSaleDto dto)
    {
        OffsiteSale? sale = await context.OffsiteSales.FindAsync(id);
        if (sale is null)
        {
            return NotFound();
        }

        sale.DressName = dto.DressName.Trim();
        sale.Era = dto.Era.Trim();
        sale.Category = dto.Category.Trim();
        sale.Size = dto.Size.Trim();
        sale.Condition = dto.Condition.Trim();
        sale.SalePrice = dto.SalePrice;
        sale.CostPrice = dto.CostPrice;
        sale.Platform = dto.Platform.Trim();
        sale.SaleDateUtc = DateTime.SpecifyKind(dto.SaleDateUtc, DateTimeKind.Utc);
        sale.Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim();

        await context.SaveChangesAsync();
        return Ok(ToDto(sale));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        OffsiteSale? sale = await context.OffsiteSales.FindAsync(id);
        if (sale is null)
        {
            return NotFound();
        }

        sale.IsDeleted = true;
        await context.SaveChangesAsync();
        return NoContent();
    }

    private static OffsiteSaleDto ToDto(OffsiteSale s) => new(
        s.Id,
        s.DressName,
        s.Era,
        s.Category,
        s.Size,
        s.Condition,
        s.SalePrice,
        s.CostPrice,
        s.Platform,
        s.SaleDateUtc,
        s.Notes
    );
}
