using Eden_Relics_BE.DTOs;

namespace Eden_Relics_BE.Services;

public interface ITopPicksService
{
    /// <summary>Public payload: the gate flag plus the curated SKUs (empty while gated).</summary>
    Task<TopPicksPublicDto> GetPublicAsync();

    /// <summary>Admin payload: the gate flag plus the full curated list (regardless of the gate).</summary>
    Task<TopPicksAdminDto> GetAdminAsync();

    /// <summary>Replace the whole curated list with the given items, in order. Returns the saved list.</summary>
    Task<TopPicksAdminDto> ReplaceAsync(IEnumerable<TopPickItemDto> items);
}
