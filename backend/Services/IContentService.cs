namespace Eden_Relics_BE.Services;

public interface IContentService
{
    Task<Dictionary<string, string>> GetAllAsync(string? locale);
    Task<Dictionary<string, string>> UpdateAllAsync(Dictionary<string, string> content);
}
