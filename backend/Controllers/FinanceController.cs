using Eden_Relics_BE.DTOs;
using Eden_Relics_BE.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eden_Relics_BE.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class FinanceController(
    IFinanceService finance,
    ImageStorageService storage,
    IWebHostEnvironment env) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<TransactionDto>>> GetAll([FromQuery] int? year, [FromQuery] int? month)
    {
        return Ok(await finance.GetAllAsync(year, month));
    }

    [HttpPost]
    public async Task<ActionResult<TransactionDto>> Create(CreateTransactionDto dto)
    {
        TransactionDto created = await finance.CreateAsync(dto);
        return CreatedAtAction(nameof(GetAll), null, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<TransactionDto>> Update(Guid id, UpdateTransactionDto dto)
    {
        TransactionDto? updated = await finance.UpdateAsync(id, dto);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        return await finance.DeleteAsync(id) ? NoContent() : NotFound();
    }

    [HttpPost("backfill-sales")]
    public async Task<ActionResult<object>> BackfillSales()
    {
        return Ok(await finance.BackfillSalesAsync());
    }

    [HttpPost("upload-receipt")]
    public async Task<ActionResult> UploadReceipt(IFormFile file)
    {
        string[] allowedExtensions = [".jpg", ".jpeg", ".png", ".webp", ".pdf"];
        string extension = Path.GetExtension(file.FileName).ToLowerInvariant();

        if (!allowedExtensions.Contains(extension))
        {
            return BadRequest(new { error = "Only image files (jpg, png, webp) and PDFs are allowed." });
        }

        if (file.Length > 10 * 1024 * 1024)
        {
            return BadRequest(new { error = "File size must be under 10MB." });
        }

        string receiptUrl = await ImageUploadHelper.ProcessAndUploadSingleAsync(
            file.OpenReadStream(), storage, env, Request, "receipts", maxWidth: 1200, maxHeight: 1600, quality: 80);

        return Ok(new { receiptUrl });
    }

    [HttpGet("summary")]
    public async Task<ActionResult> GetSummary()
    {
        return Ok(await finance.GetSummaryAsync());
    }

    [HttpGet("pnl")]
    public async Task<ActionResult<AccountingSnapshot>> GetPnl()
    {
        return Ok(await finance.GetPnlAsync());
    }

    [HttpGet("export")]
    public async Task<IActionResult> Export([FromQuery] int? year, [FromQuery] int? month)
    {
        FinanceExportFile export = await finance.ExportAsync(year, month);
        return File(export.Content, "text/csv", export.FileName);
    }
}
