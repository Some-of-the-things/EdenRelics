using Eden_Relics_BE.Data.Entities;
using Eden_Relics_BE.DTOs;
using Eden_Relics_BE.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Eden_Relics_BE.Services;

/// <summary>
/// Offsite sales — pieces sold on Vinted/Depop/eBay that were never catalogue products.
/// </summary>
/// <remarks>
/// Each sale also writes to the Transactions ledger, because the admin Finance tab reads the
/// ledger and nothing else. Recording a sale here used to leave Finance untouched, so total
/// income silently ignored every offsite sale — reported 2026-08-19. The ledger already keys
/// COGS separately from income for catalogue products, so offsite sales do the same: an income
/// row at the price it actually sold for, and a cost row, both keyed on the sale's id so
/// re-running is idempotent and either can be repaired independently.
/// </remarks>
public class OffsiteSaleService(
    IRepository<OffsiteSale> repository,
    IRepository<Transaction> transactions) : IOffsiteSaleService
{
    /// <summary>Ledger Reference for the income row of an offsite sale.</summary>
    public static string IncomeRef(Guid saleId) => $"offsite:{saleId}";

    /// <summary>Ledger Reference for the cost-of-goods row of an offsite sale.</summary>
    public static string CogsRef(Guid saleId) => $"offsite-cogs:{saleId}";

    public async Task<List<OffsiteSaleDto>> GetAllAsync()
    {
        IEnumerable<OffsiteSale> sales = await repository.GetAllAsync();
        return sales
            .OrderByDescending(s => s.SaleDateUtc)
            .Select(ToDto)
            .ToList();
    }

    public async Task<OffsiteSaleDto> CreateAsync(CreateOffsiteSaleDto dto)
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

        await repository.AddAsync(sale);
        await SyncLedgerAsync(sale);
        return ToDto(sale);
    }

    public async Task<OffsiteSaleDto?> UpdateAsync(Guid id, CreateOffsiteSaleDto dto)
    {
        OffsiteSale? sale = await repository.GetByIdAsync(id);
        if (sale is null)
        {
            return null;
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

        await repository.UpdateAsync(sale);
        // Correcting the price is the whole point of editing a sale, so the ledger has to follow
        // it. Without this the Finance total keeps the figure that was first typed.
        await SyncLedgerAsync(sale);
        return ToDto(sale);
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        OffsiteSale? sale = await repository.GetByIdAsync(id);
        if (sale is null)
        {
            return false;
        }

        // Delete the ledger rows too, or income keeps counting a sale that no longer exists.
        IEnumerable<Transaction> ledger = await LedgerRowsFor(id);
        await transactions.RemoveRangeAsync(ledger);

        await repository.DeleteAsync(id);
        return true;
    }

    /// <summary>
    /// Brings the sale's two ledger rows in line with the sale: creates them if missing, updates
    /// them if the sale changed, and removes the cost row if the cost price has gone to zero.
    /// </summary>
    private async Task SyncLedgerAsync(OffsiteSale sale)
    {
        List<Transaction> existing = (await LedgerRowsFor(sale.Id)).ToList();

        string incomeRef = IncomeRef(sale.Id);
        Transaction? income = existing.FirstOrDefault(t => t.Reference == incomeRef);
        if (income is null)
        {
            await transactions.AddAsync(new Transaction
            {
                Date = sale.SaleDateUtc,
                Description = $"Sale: {sale.DressName}",
                Amount = sale.SalePrice,
                Category = TransactionCategories.Sales,
                Platform = sale.Platform,
                Reference = incomeRef,
            });
        }
        else
        {
            income.Date = sale.SaleDateUtc;
            income.Description = $"Sale: {sale.DressName}";
            income.Amount = sale.SalePrice;
            income.Platform = sale.Platform;
            await transactions.UpdateAsync(income);
        }

        string cogsRef = CogsRef(sale.Id);
        Transaction? cogs = existing.FirstOrDefault(t => t.Reference == cogsRef);
        if (sale.CostPrice <= 0)
        {
            // No cost recorded (or it was cleared) — a zero-value expense row would only be noise.
            if (cogs is not null)
            {
                await transactions.RemoveRangeAsync([cogs]);
            }
            return;
        }

        if (cogs is null)
        {
            await transactions.AddAsync(new Transaction
            {
                // Same date as the income row, so both land in the month the piece sold and the
                // monthly profit figure is a like-for-like subtraction.
                Date = sale.SaleDateUtc,
                Description = $"Cost of goods: {sale.DressName}",
                Amount = -sale.CostPrice,
                Category = TransactionCategories.Stock,
                Platform = sale.Platform,
                Reference = cogsRef,
            });
        }
        else
        {
            cogs.Date = sale.SaleDateUtc;
            cogs.Description = $"Cost of goods: {sale.DressName}";
            cogs.Amount = -sale.CostPrice;
            cogs.Platform = sale.Platform;
            await transactions.UpdateAsync(cogs);
        }
    }

    private async Task<IEnumerable<Transaction>> LedgerRowsFor(Guid saleId)
    {
        string incomeRef = IncomeRef(saleId);
        string cogsRef = CogsRef(saleId);
        return await transactions.Query()
            .Where(t => t.Reference == incomeRef || t.Reference == cogsRef)
            .ToListAsync();
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
