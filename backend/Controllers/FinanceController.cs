using System.Text;
using Eden_Relics_BE.Data;
using Eden_Relics_BE.Data.Entities;
using Eden_Relics_BE.DTOs;
using Eden_Relics_BE.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Eden_Relics_BE.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class FinanceController(
    EdenRelicsDbContext context,
    ImageStorageService storage,
    IWebHostEnvironment env) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<TransactionDto>>> GetAll(
        [FromQuery] int? year, [FromQuery] int? month)
    {
        IQueryable<Transaction> query = context.Transactions.OrderByDescending(t => t.Date);

        if (year.HasValue)
        {
            query = query.Where(t => t.Date.Year == year.Value);
        }

        if (month.HasValue)
        {
            query = query.Where(t => t.Date.Month == month.Value);
        }

        List<TransactionDto> transactions = await query
            .Select(t => ToDto(t))
            .ToListAsync();

        return Ok(transactions);
    }

    [HttpPost]
    public async Task<ActionResult<TransactionDto>> Create(CreateTransactionDto dto)
    {
        Transaction transaction = new()
        {
            Date = dto.Date,
            Description = dto.Description,
            Amount = dto.Amount,
            Category = dto.Category,
            Platform = dto.Platform,
            Reference = dto.Reference,
            Notes = dto.Notes,
        };

        context.Transactions.Add(transaction);
        await context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetAll), null, ToDto(transaction));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<TransactionDto>> Update(Guid id, UpdateTransactionDto dto)
    {
        Transaction? transaction = await context.Transactions.FindAsync(id);
        if (transaction is null)
        {
            return NotFound();
        }

        if (dto.Date.HasValue) { transaction.Date = dto.Date.Value; }
        if (dto.Description is not null) { transaction.Description = dto.Description; }
        if (dto.Amount.HasValue) { transaction.Amount = dto.Amount.Value; }
        if (dto.Category is not null) { transaction.Category = dto.Category; }
        if (dto.Platform is not null) { transaction.Platform = dto.Platform == "" ? null : dto.Platform; }
        if (dto.Reference is not null) { transaction.Reference = dto.Reference == "" ? null : dto.Reference; }
        if (dto.ReceiptUrl is not null) { transaction.ReceiptUrl = dto.ReceiptUrl == "" ? null : dto.ReceiptUrl; }
        if (dto.Notes is not null) { transaction.Notes = dto.Notes == "" ? null : dto.Notes; }

        await context.SaveChangesAsync();

        return Ok(ToDto(transaction));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        Transaction? transaction = await context.Transactions.FindAsync(id);
        if (transaction is null)
        {
            return NotFound();
        }

        transaction.IsDeleted = true;
        await context.SaveChangesAsync();

        return NoContent();
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

        string receiptUrl = await ImageUploadHelper.ProcessAndUploadAsync(
            file.OpenReadStream(), storage, env, Request, "receipts", maxWidth: 1200, maxHeight: 1600, quality: 80);

        return Ok(new { receiptUrl });
    }

    [HttpGet("summary")]
    public async Task<ActionResult> GetSummary()
    {
        List<Transaction> all = await context.Transactions.ToListAsync();

        var byMonth = all
            .GroupBy(t => new { t.Date.Year, t.Date.Month })
            .OrderByDescending(g => g.Key.Year).ThenByDescending(g => g.Key.Month)
            .Select(g =>
            {
                decimal income = g.Where(t => t.Amount > 0).Sum(t => t.Amount);
                decimal expenses = g.Where(t => t.Amount < 0).Sum(t => Math.Abs(t.Amount));
                return new
                {
                    month = $"{g.Key.Year}-{g.Key.Month:D2}",
                    income,
                    expenses,
                    profit = income - expenses,
                    count = g.Count(),
                    byCategory = g
                        .GroupBy(t => t.Category)
                        .Select(c => new
                        {
                            category = c.Key,
                            total = c.Sum(t => t.Amount),
                            count = c.Count(),
                        })
                        .OrderByDescending(c => Math.Abs(c.total))
                        .ToList(),
                    byPlatform = g
                        .Where(t => t.Platform is not null)
                        .GroupBy(t => t.Platform!)
                        .Select(p => new
                        {
                            platform = p.Key,
                            total = p.Sum(t => t.Amount),
                            count = p.Count(),
                        })
                        .OrderByDescending(p => Math.Abs(p.total))
                        .ToList(),
                };
            })
            .ToList();

        decimal totalIncome = all.Where(t => t.Amount > 0).Sum(t => t.Amount);
        decimal totalExpenses = all.Where(t => t.Amount < 0).Sum(t => Math.Abs(t.Amount));

        return Ok(new
        {
            totalIncome = Math.Round(totalIncome, 2),
            totalExpenses = Math.Round(totalExpenses, 2),
            totalProfit = Math.Round(totalIncome - totalExpenses, 2),
            transactionCount = all.Count,
            byMonth,
        });
    }

    [HttpGet("export")]
    public async Task<IActionResult> Export([FromQuery] int? year, [FromQuery] int? month)
    {
        IQueryable<Transaction> query = context.Transactions.OrderByDescending(t => t.Date);

        if (year.HasValue)
        {
            query = query.Where(t => t.Date.Year == year.Value);
        }

        if (month.HasValue)
        {
            query = query.Where(t => t.Date.Month == month.Value);
        }

        List<Transaction> transactions = await query.ToListAsync();

        StringBuilder csv = new();
        csv.AppendLine("Date,Description,Amount,Category,Platform,Reference,Notes");
        foreach (Transaction t in transactions)
        {
            csv.AppendLine(
                $"{t.Date:yyyy-MM-dd}," +
                $"\"{t.Description.Replace("\"", "\"\"")}\"," +
                $"{t.Amount}," +
                $"\"{t.Category}\"," +
                $"\"{t.Platform ?? ""}\"," +
                $"\"{t.Reference ?? ""}\"," +
                $"\"{(t.Notes ?? "").Replace("\"", "\"\"")}\"");
        }

        string fileName = year.HasValue && month.HasValue
            ? $"transactions-{year}-{month:D2}.csv"
            : year.HasValue
                ? $"transactions-{year}.csv"
                : "transactions-all.csv";

        return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", fileName);
    }

    private static TransactionDto ToDto(Transaction t) => new(
        t.Id, t.Date, t.Description, t.Amount, t.Category,
        t.Platform, t.Reference, t.ReceiptUrl, t.Notes, t.CreatedAtUtc);
}
