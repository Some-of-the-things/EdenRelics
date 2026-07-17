using Eden_Relics_BE.Data.Entities;
using Eden_Relics_BE.DTOs;
using Eden_Relics_BE.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Eden_Relics_BE.Services;

/// <summary>
/// Curated "Our Top Picks" edit. Persistence is a small, admin-curated list of <see cref="TopPick"/>
/// rows (product ID + order + featured flag), replaced wholesale on save. The public read is gated by
/// <see cref="TopPicksOptions.Enabled"/> and fails closed: while the gate is off, callers get an
/// empty selection so no public surface appears, even though the admin list is still editable.
/// Keyed by product ID so picks stay unambiguous across sellers once the marketplace is live.
/// </summary>
public class TopPicksService(
    IRepository<TopPick> picks,
    IOptions<TopPicksOptions> options) : ITopPicksService
{
    private bool Enabled => options.Value.Enabled;

    public async Task<TopPicksPublicDto> GetPublicAsync()
    {
        if (!Enabled)
        {
            return new TopPicksPublicDto(false, [], []);
        }

        List<TopPick> ordered = await OrderedAsync();
        List<Guid> ids = ordered.Select(p => p.ProductId).ToList();
        List<Guid> featured = ordered.Where(p => p.Featured).Select(p => p.ProductId).ToList();
        return new TopPicksPublicDto(true, ids, featured);
    }

    public async Task<TopPicksAdminDto> GetAdminAsync()
    {
        List<TopPick> ordered = await OrderedAsync();
        return new TopPicksAdminDto(Enabled, ordered.Select(p => new TopPickItemDto(p.ProductId, p.Featured)).ToList());
    }

    public async Task<TopPicksAdminDto> ReplaceAsync(IEnumerable<TopPickItemDto> items)
    {
        // De-dupe by product (first wins) and drop empties, preserving the submitted order.
        List<TopPickItemDto> clean = [];
        HashSet<Guid> seen = [];
        foreach (TopPickItemDto item in items)
        {
            if (item.ProductId != Guid.Empty && seen.Add(item.ProductId))
            {
                clean.Add(item);
            }
        }

        // Full replace: TopPick is IHardDeletable, so old rows are physically removed. The removals
        // and inserts commit together in the single SaveChanges triggered by AddRangeAsync.
        IEnumerable<TopPick> existing = await picks.GetAllAsync();
        await picks.RemoveRangeAsync(existing);

        List<TopPick> fresh = clean
            .Select((item, index) => new TopPick { ProductId = item.ProductId, Position = index, Featured = item.Featured })
            .ToList();
        await picks.AddRangeAsync(fresh);

        return new TopPicksAdminDto(Enabled, clean);
    }

    private async Task<List<TopPick>> OrderedAsync() =>
        await picks.Query().OrderBy(p => p.Position).ToListAsync();
}
