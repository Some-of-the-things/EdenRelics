using Eden_Relics_BE.DTOs;

namespace Eden_Relics_BE.Services;

public interface IHomeSectionsService
{
    /// <summary>Public: the visible sections in display order. Top Picks is always included; the
    /// frontend applies its own switch.</summary>
    Task<List<HomeSectionDto>> GetPublicAsync();

    /// <summary>Admin: every section, hidden ones included, in display order.</summary>
    Task<List<HomeSectionDto>> GetAdminAsync();

    /// <summary>
    /// Replace every section with the given list, in order. Returns the saved list.
    /// Throws <see cref="HomeSectionValidationException"/> when the list is invalid; nothing is saved.
    /// </summary>
    Task<List<HomeSectionDto>> ReplaceAsync(IReadOnlyList<HomeSectionDto> sections);
}

public class HomeSectionValidationException(string message) : Exception(message);
