using Eden_Relics_BE.DTOs;

namespace Eden_Relics_BE.Services;

public interface IOffsiteSaleService
{
    Task<List<OffsiteSaleDto>> GetAllAsync();
    Task<OffsiteSaleDto> CreateAsync(CreateOffsiteSaleDto dto);
    Task<OffsiteSaleDto?> UpdateAsync(Guid id, CreateOffsiteSaleDto dto);
    Task<bool> DeleteAsync(Guid id);
}
