using Eden_Relics_BE.Data.Entities;
using Eden_Relics_BE.DTOs;
using Eden_Relics_BE.Repositories;

namespace Eden_Relics_BE.Services;

public class OffsiteSaleService(IRepository<OffsiteSale> repository) : IOffsiteSaleService
{
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
        return ToDto(sale);
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        OffsiteSale? sale = await repository.GetByIdAsync(id);
        if (sale is null)
        {
            return false;
        }

        await repository.DeleteAsync(id);
        return true;
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
